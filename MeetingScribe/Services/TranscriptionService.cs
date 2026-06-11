using NAudio.Wave;
using System;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

public class TranscriptionService
{
    // Событие для передачи текста в UI
    public event Action<string>? TextRecognized;

    private readonly SemaphoreSlim _whisperSemaphore = new(1, 1);
    private WhisperProcessor? _processor;
    //private SileroVadDetector? _vad;
    private WaveInEvent? _waveIn;
    // ... (копируем логику накопления буфера из вашего примера)

    public async Task InitializeAsync()
    {
        // Инициализация моделей (Whisper + VAD)
    }

    public void StartRecording()
    {
        // Запуск WaveIn и VAD цикла
    }

    public void StopRecording()
    {
        _waveIn?.StopRecording();
    }
}