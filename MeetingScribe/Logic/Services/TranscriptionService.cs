using MeetingScribe.Logic.Meeting;
using MeetingScribe.UILogic;
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
    // --- Properties ---
    public string? CurrentMeetingFolder { get; set; }
    public AppSettings ActiveSettings { get; set; } = new();

    // Allow MainWindowViewModel to listen for raw audio to save the FULL record
    public event Action<float[]>? RawAudioCaptured;
    public event Action<float[]>? BoostedAudioCaptured;
    // Event for UI to show the volume meter (0.0 to 1.0)
    public event Action<float>? VolumeLevelChanged;

    /// <summary>
    /// Loads AI models and initializes engines.
    /// </summary>
    public async Task InitializeAsync(string whisperModelPath, string transcriptionLanguage, string initialPrompt = "", bool useAllProcessor = false)
    {
        if (!File.Exists(whisperModelPath))
            throw new FileNotFoundException("AI Model files not found. Check Assets folder.");
        // If models are already loaded, unload them first to free resources
        if (_whisperFactory != null) UnloadModel();
        // Determine how many threads to use for Whisper based on user settings
        int threadsToUse = useAllProcessor ? Environment.ProcessorCount : Math.Max(1, Environment.ProcessorCount / 2);

        await Task.Run(() =>
        {
            StatusChanged?.Invoke("Initializing AI Engines...");

            // Initialize Whisper Factory and Processor
            _whisperFactory = WhisperFactory.FromPath(whisperModelPath);
            _whisperProcessor = _whisperFactory.CreateBuilder()
                .WithLanguage(transcriptionLanguage)
                .WithPrompt(initialPrompt)
                .WithThreads(threadsToUse)
                .Build();

            // Initialize Voice Activity Detector
            string vadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Models", "silero_vad.onnx");
            if (_vadDetector == null && File.Exists(vadPath))
            {
                _vadDetector = new SileroVadDetector(vadPath);
            }

            StatusChanged?.Invoke("Ready to Record");
        });
    }

    /// <summary>
    /// Processes an entire audio file offline for maximum quality.
    /// </summary>
    public async Task<List<TranscriptLine>> ProcessFileAsync(string filePath, IProgress<double> progress)
    {
        if (_whisperProcessor == null)
            throw new InvalidOperationException("Whisper Processor is not initialized. Call InitializeAsync first.");

        var result = new List<TranscriptLine>();

        using var fileStream = File.OpenRead(filePath);
        var totalLength = fileStream.Length;

        // Process the stream segment by segment
        await foreach (var segment in _whisperProcessor.ProcessAsync(fileStream))
        {
            result.Add(new TranscriptLine
            {
                // Format time as [HH:mm:ss]
                Timestamp = $"[{segment.Start:hh\\:mm\\:ss}]",
                SpeakerName = "Speaker", // Diarization will be added later
                Text = segment.Text.Trim()
            });

            // Report progress back to UI
            if (totalLength > 0)
            {
                double currentProgress = (double)fileStream.Position / totalLength * 100;
                progress.Report(currentProgress);
            }
        }

        return result;
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

                if (prob > ActiveSettings.SpeechThreshold)
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

                    if (silenceMs >= ActiveSettings.SilenceTimeoutMs)
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
        float[] rawSamples = new float[sampleCount];
        float[] boostedSamples = new float[sampleCount];
        float maxAbs = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(buffer, i * 2);
            // --- Raw audio ---
            float normalized = sample / 32768.0f;
            rawSamples[i] = normalized;

            // --- Boosted audio ---
            float boosted = normalized * ActiveSettings.AudioGain;
            // Soft clipping formula: simple cubic limiter
            if (boosted > 1.0f) boosted = 1.0f;
            else if (boosted < -1.0f) boosted = -1.0f;
            else boosted = boosted - (float)Math.Pow(boosted, 3) / 3; // Soften the peaks

            boostedSamples[i] = Math.Clamp(boosted, -1.0f, 1.0f);

            // Track peak for the meter
            float abs = Math.Abs(boostedSamples[i]);
            if (abs > maxAbs) maxAbs = abs;
        }
        // Record to file the raw and boosted audio for the full meeting
        RawAudioCaptured?.Invoke(rawSamples);
        BoostedAudioCaptured?.Invoke(boostedSamples);

        // Raise event with the peak level
        VolumeLevelChanged?.Invoke(maxAbs);

        return boostedSamples;
    }

    public void UnloadModel()
    {
        // Destroying the thread and the factory—this will free up memory/video memory
        _whisperProcessor?.Dispose();
        _whisperProcessor = null;

        _whisperFactory?.Dispose();
        _whisperFactory = null;

        // We trigger a garbage collection (GC)
        GC.Collect();
        GC.WaitForPendingFinalizers();
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