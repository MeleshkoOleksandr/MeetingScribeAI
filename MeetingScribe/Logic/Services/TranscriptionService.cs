using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace MeetingScribe.Logic.Services;

/// <summary>
/// Service responsible for capturing audio, detecting speech (VAD), 
/// and transcribing it using Whisper.net.
/// </summary>
public class TranscriptionService : IDisposable
{
    // --- Events ---
    public event Action<string>? TranscriptionUpdated;
    public event Action<string>? StatusChanged;

    // --- AI Engines ---
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private SileroVadDetector? _vadDetector;

    // --- Audio Capture ---
    private WaveInEvent? _waveIn;
    private readonly List<byte> _incomingAudioBytes = new();
    private readonly List<float> _speechBuffer = new();

    // --- State Management ---
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _whisperSemaphore = new(1, 1);
    private bool _isRecording;
    private bool _isSpeakingNow;
    private int _silenceSamplesCount;

    // --- Constants ---
    private const int SampleRate = 16000;

    private const string Language = "it"; // Change to "it", "ru" or "auto" as needed
    private const float SpeechThreshold = 0.3f;    // VAD sensitivity (0.0 to 1.0)
    private const int SilenceTimeoutMs = 600;     // Pause duration to trigger transcription
    private const float AudioGain = 5.0f;          // Boost for quiet microphones

    /// <summary>
    /// Loads AI models and initializes engines.
    /// </summary>
    /// <param name="whisperModelPath">Path to ggml-base.bin or similar</param>
    /// <param name="vadModelPath">Path to silero_vad.onnx</param>
    public async Task InitializeAsync(string whisperModelPath, string vadModelPath)
    {
        if (!File.Exists(whisperModelPath) || !File.Exists(vadModelPath))
            throw new FileNotFoundException("AI Model files not found. Check Assets folder.");

        await Task.Run(() =>
        {
            StatusChanged?.Invoke("Initializing AI Engines...");

            // Initialize Whisper Factory and Processor
            _whisperFactory = WhisperFactory.FromPath(whisperModelPath);
            _whisperProcessor = _whisperFactory.CreateBuilder()
                .WithLanguage(Language) // Change to "it", "ru" or "auto" as needed
                .WithThreads(2)
                .Build();

            // Initialize Voice Activity Detector
            _vadDetector = new SileroVadDetector(vadModelPath);

            StatusChanged?.Invoke("Ready to Record");
        });
    }

    /// <summary>
    /// Starts the microphone capture and VAD processing loop.
    /// </summary>
    public void Start()
    {
        if (_isRecording) return;

        _isRecording = true;
        _cts = new CancellationTokenSource();
        _incomingAudioBytes.Clear();
        _speechBuffer.Clear();
        _silenceSamplesCount = 0;
        _isSpeakingNow = false;
        _vadDetector?.ResetState();

        // 1. Setup Microphone (16kHz, 16-bit, Mono)
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SampleRate, 16, 1),
            BufferMilliseconds = 100
        };

        _waveIn.DataAvailable += (s, a) =>
        {
            lock (_incomingAudioBytes)
            {
                _incomingAudioBytes.AddRange(a.Buffer.Take(a.BytesRecorded));
            }
        };

        _waveIn.StartRecording();
        StatusChanged?.Invoke("Recording...");

        // 2. Start background VAD processing loop
        Task.Run(() => ProcessingLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the recording and cancels the processing loop.
    /// </summary>
    public void Stop()
    {
        _isRecording = false;
        _cts?.Cancel();
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
        StatusChanged?.Invoke("Stopped");
    }

    /// <summary>
    /// Main loop that extracts audio chunks and runs VAD.
    /// </summary>
    private async Task ProcessingLoopAsync(CancellationToken token)
    {
        // Silero VAD requires exactly 480 samples (30ms) for 16kHz audio
        const int chunkSize = 480;
        const int bytesNeeded = chunkSize * 2; // 16-bit = 2 bytes per sample

        try
        {
            while (!token.IsCancellationRequested)
            {
                byte[]? rawChunk = null;

                lock (_incomingAudioBytes)
                {
                    if (_incomingAudioBytes.Count >= bytesNeeded)
                    {
                        rawChunk = _incomingAudioBytes.GetRange(0, bytesNeeded).ToArray();
                        _incomingAudioBytes.RemoveRange(0, bytesNeeded);
                    }
                }

                if (rawChunk == null)
                {
                    await Task.Delay(10, token);
                    continue;
                }

                // Convert bytes to float samples with Gain
                float[] samples = ConvertToFloat(rawChunk);

                // Run VAD Engine
                float prob = _vadDetector?.IsSpeechProbability(samples) ?? 0f;

                if (prob > SpeechThreshold)
                {
                    if (!_isSpeakingNow) _isSpeakingNow = true;
                    _silenceSamplesCount = 0;
                    _speechBuffer.AddRange(samples);
                }
                else if (_isSpeakingNow)
                {
                    // User is silent, but we keep adding samples for context
                    _speechBuffer.AddRange(samples);
                    _silenceSamplesCount += samples.Length;

                    double silenceMs = (_silenceSamplesCount / (double)SampleRate) * 1000;

                    if (silenceMs >= SilenceTimeoutMs)
                    {
                        // Phrase finished -> Send to Whisper
                        _isSpeakingNow = false;
                        _silenceSamplesCount = 0;

                        if (_speechBuffer.Count > SampleRate * 0.5) // Ignore noises shorter than 0.5s
                        {
                            float[] phraseToProcess = _speechBuffer.ToArray();
                            _speechBuffer.Clear();
                            _ = Task.Run(() => TranscribeAsync(phraseToProcess, token), token);
                        }
                        else
                        {
                            _speechBuffer.Clear();
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* Normal exit */ }
    }

    StringBuilder test = new StringBuilder();

    /// <summary>
    /// Sends a detected speech segment to the Whisper engine.
    /// </summary>
    private async Task TranscribeAsync(float[] audioData, CancellationToken token)
    {
        if (_whisperProcessor == null) return;

        // Ensure only one transcription runs at a time on the GPU/CPU
        await _whisperSemaphore.WaitAsync(token);

        try
        {
            var sb = new StringBuilder();
            await foreach (var result in _whisperProcessor.ProcessAsync(audioData, token))
            {
                sb.Append(result.Text);
            }

            string finalResult = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalResult))
            {
                TranscriptionUpdated?.Invoke(finalResult);
                 test.Append(finalResult + " -+-  ");
            }
        }
        finally
        {
            _whisperSemaphore.Release();
        }
    }

    /// <summary>
    /// Converts raw PCM bytes to normalized float samples [-1.0, 1.0].
    /// </summary>
    private float[] ConvertToFloat(byte[] buffer)
    {
        int sampleCount = buffer.Length / 2;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(buffer, i * 2);
            float normalized = sample / 32768.0f;
            float boosted = normalized * AudioGain;
            samples[i] = Math.Clamp(boosted, -1.0f, 1.0f);
        }
        return samples;
    }

    public void Dispose()
    {
        Stop();
        _whisperProcessor?.Dispose();
        _whisperFactory?.Dispose();
        _vadDetector?.Dispose();
        _whisperSemaphore.Dispose();
    }
}