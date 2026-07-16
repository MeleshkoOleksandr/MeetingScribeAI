using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;

using NAudio.Wave;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.Meeting;

public class MeetingManager
{
    private readonly TranscriptionService _transcriptionService;
    private WaveFileWriter? _fullAudioWriter;
    private WaveFileWriter? _boostedAudioWriter;
    private string _currentFolderPath = "";

    public MeetingSession? CurrentSession { get; private set; }

    public MeetingManager(TranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
    }

    public async Task<MeetingSession> InitializeMeeting(MeetingSession currentSession, AppSettings settings)
    {
        CurrentSession = currentSession;

        // 1. Create a folder
        string archiveRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Meeting Archive");
        _currentFolderPath = Path.Combine(archiveRoot, CurrentSession.Name);
        Directory.CreateDirectory(_currentFolderPath);
        CurrentSession.FolderPath = _currentFolderPath;

        // 2. Configure the log files
        _fullAudioWriter = new WaveFileWriter(Path.Combine(_currentFolderPath, "full_record.wav"), new WaveFormat(16000, 16, 1));
        _boostedAudioWriter = new WaveFileWriter(Path.Combine(_currentFolderPath, "boosted_record.wav"), new WaveFormat(16000, 16, 1));

        // 3. Subscribe to threads
        _transcriptionService.RawAudioCaptured += (s) => _fullAudioWriter?.WriteSamples(s, 0, s.Length);
        _transcriptionService.BoostedAudioCaptured += (s) => _boostedAudioWriter?.WriteSamples(s, 0, s.Length);

        // 4. Init Whisper
        string whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "WhisperModels", settings.SelectedModel);
        await _transcriptionService.InitializeAsync(whisperPath, CurrentSession.Language);

        return CurrentSession;
    }

    public void Start() => _transcriptionService.Start();

    public void Stop()
    {
        _transcriptionService.Stop();

        _fullAudioWriter?.Dispose();
        _fullAudioWriter = null;
        _boostedAudioWriter?.Dispose();
        _boostedAudioWriter = null;

        _transcriptionService.UnloadModel();
    }

    public void SaveSession(TimeSpan duration, ObservableCollection<TranscriptLine> lines)
    {
        if (CurrentSession == null) return;

        CurrentSession.Duration = duration;
        CurrentSession.FullTranscript = new ObservableCollection<TranscriptLine>(lines);

        string jsonPath = Path.Combine(_currentFolderPath, "session_data.json");
        string json = JsonSerializer.Serialize(CurrentSession, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
    }


    public async Task<MeetingSession> CreateSessionFromAudioFile(string sourceFilePath, AppSettings settings)
    {
        // 1. Create Metadata
        var session = new MeetingSession
        {
            Name = Path.GetFileNameWithoutExtension(sourceFilePath),
            Language = settings.TranscriptionLanguage,
            StartTime = DateTime.Now
        };

        // 2. Prepare Folder
        string archiveRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Meeting Archive");
        string folderName = $"[{DateTime.Now:yy-MM-dd}] - {session.Name}";
        string sessionFolder = Path.Combine(archiveRoot, folderName);
        Directory.CreateDirectory(sessionFolder);
        session.FolderPath = sessionFolder;

        // 3. Transcode to standard format (16kHz, Mono, 16-bit)
        // Whisper works best with this specific format
        string targetPath = Path.Combine(sessionFolder, "boosted_record.wav");

        await Task.Run(() =>
        {
            using (var reader = new AudioFileReader(sourceFilePath))
            {
                // Setting up meeting duration
                session.Duration = reader.TotalTime;
                // Resample to 16000Hz and convert to Mono
                var outFormat = new WaveFormat(16000, 16, 1);
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    // High quality resampling
                    resampler.ResamplerQuality = 60;
                    WaveFileWriter.CreateWaveFile(targetPath, resampler);
                }
            }
        });

        // 4. Save Initial JSON (Metadata only)
        string jsonPath = Path.Combine(sessionFolder, "session_data.json");
        string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);

        return session;
    }
}