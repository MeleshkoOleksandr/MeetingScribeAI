using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;
using NAudio.Wave;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


namespace MeetingScribe.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //  -- ══════ Fields & Properties  ══════ --//

    //   ---   Navigation state 
    public PageList PageList { get; } = new();     // List of pages used in app (static and temporary) and there methods 
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isSidebarExpanded = true;

    // We use a custom property for SelectedMenuItem to prevent unwanted null assignment from UI. Shows current selected page in the sidebar.
    private NavigationItem? _selectedMenuItem;
    public NavigationItem? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            // Ignore null if we already have a selection to prevent multi-list conflicts
            if (value == null && _selectedMenuItem != null) return;

            if (SetProperty(ref _selectedMenuItem, value))
            {
                // Call the regular method instead of a partial one
                HandleNavigation(value);
            }
        }
    }

    //   ---   Audio recording and speech recognition state
    private readonly TranscriptionService _transcriptionService = new();
    private DispatcherTimer? _timer;
    private DateTime _startTime;
    private MeetingSession? _currentSession;

    [ObservableProperty] private string _elapsedTime = "00:00:00";
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isPaused;

    //   ---   Meeting archive state
    private string _currentMeetingFolderPath = "";
    private WaveFileWriter? _fullAudioWriter;


    // -- ══════ Navigation  ══════ --//

    private void HandleNavigation(NavigationItem? value)
    {
        if (value == null) return;
        CurrentPage = value.Page;
    }

    // Logic for programmatic navigation
    private void Navigate(NavigationItem page)
    {
        SelectedMenuItem = page;
    }

    public void Navigate(PageNames target)
    {
        var item = PageList.GetByTarget(target);
        if (item != null) SelectedMenuItem = item;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    // -- ══════ Meetings functions & Recording  ══════ --//

    // Command triggered by the "Start Recording" button in NewMeetingView
    [RelayCommand]
    private async Task StartMeeting()
    {
        // Check if we are currently on the New Meeting setup page
        if (CurrentPage is not NewMeetingViewModel setupPage) return;

        try
        {
            //  Get Settings from the Settings Page
            var settingsItem = PageList.GetByTarget(PageNames.Settings);
            var settingsVm = settingsItem?.Page as SettingsViewModel;
            _currentSession = setupPage.GetSessionData();

            //  Setup Session & Folder
            var activeSettings = settingsVm?.Settings ?? new AppSettings();
            activeSettings.TranscriptionLanguage = _currentSession.Language;
            string datePrefix = DateTime.Now.ToString("yy-MM-dd");
            string folderName = _currentSession.Name;
            string archiveRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Meeting Archive");
            string sessionFolder = Path.Combine(archiveRoot, folderName);
            if (!Directory.Exists(sessionFolder)) Directory.CreateDirectory(sessionFolder);

            // Configure Service
            _transcriptionService.ActiveSettings = activeSettings;
            _transcriptionService.CurrentMeetingFolder = sessionFolder;

            // Full Audio Recording Setup
            string fullAudioPath = Path.Combine(sessionFolder, "full_audio.wav");
            var audioWriter = new WaveFileWriter(fullAudioPath, new WaveFormat(16000, 16, 1));
            _transcriptionService.RawAudioCaptured += (samples) => audioWriter.WriteSamples(samples, 0, samples.Length);

            // Prepare AI Model Paths
            // Ensure these folders and files exist in your Output directory!
            string whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "WhisperModels", activeSettings.SelectedModel);
            string vadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Models", "silero_vad.onnx");

            // Initialize Whisper and VAD engines
            // This might take a few seconds depending on GPU/CPU speed
            await _transcriptionService.InitializeAsync(whisperPath, vadPath);

            // Prepare Folders
            PrepareMeetingFolder(_currentSession.Name);
            // Initialize Full Audio Recording
            string audioPath = Path.Combine(_currentMeetingFolderPath, "full_record.wav");
            _fullAudioWriter = new WaveFileWriter(audioPath, new WaveFormat(16000, 16, 1));

            // Create the Live Page (ActiveMeetingViewModel)
            var activeVm = new ActiveMeetingViewModel(_currentSession.Name);
            // Create a Temporary Navigation Item for the Sidebar
            var liveNavItem = new NavigationItem
            {
                Label = "Meeting Recording",
                Icon = "Waveform",
                Target = PageNames.Recording,
                Description = _currentSession.Name,
                IsStartUp = false,
                Page = activeVm
            };

            // Add to the dynamic sidebar list and navigate to it
            PageList.AddTemporaryItem(liveNavItem);
            Navigate(liveNavItem);

            // Subscribe to transcription events
            // We use a separate method to handle incoming text segments
            _transcriptionService.TranscriptionUpdated -= OnNewTextReceived; // Clean up old subs
            _transcriptionService.TranscriptionUpdated += OnNewTextReceived;

            // Start the Audio Engine and VAD loop
            _transcriptionService.Start();

            // Start the Elapsed Time timer
            _startTime = DateTime.Now;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;
                ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
            };
            _timer.Start();

            // Trigger UI state (Shows the Bottom Control Bar)
            IsRecording = true;
            IsPaused = false;
        }
        catch (Exception ex)
        {
            // Log or show error (e.g., if models are missing or GPU failed)
            System.Diagnostics.Debug.WriteLine($"Error starting meeting: {ex.Message}");
        }
    }

    /// <summary>
    /// This method is called every time Whisper finishes transcribing a speech segment.
    /// </summary>
    private void OnNewTextReceived(string text)
    {
        // Important: Redirect the text to the ActiveMeeting page only if it's currently active
        if (CurrentPage is ActiveMeetingViewModel activeVm)
        {
            activeVm.AddLine(ElapsedTime, text);
        }
    }

    [RelayCommand]
    private void StopMeeting()
    {
        // Stop the transcription and audio recording
        IsRecording = false;
        _transcriptionService.Stop();
        _timer?.Stop();
        _fullAudioWriter?.Dispose();
        _fullAudioWriter = null;

        // Finalize the session data and save it as JSON in the meeting folder
        if (_currentSession != null && CurrentPage is ActiveMeetingViewModel activeVm)
        {
            // Save the transcript to the session object
            _currentSession.FullTranscript = activeVm.TranscriptLines.ToList();
            _currentSession.Duration = DateTime.Now - _startTime;

            // Save Session as JSON
            string jsonPath = Path.Combine(_currentMeetingFolderPath, "session_data.json");
            string json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
        }

        // Clean up UI
        var recordingItem = PageList.GetByTarget(PageNames.Recording);
        if (recordingItem != null) PageList.RemoveTemporaryItem(recordingItem);

        //  Change later for a post-meeting review page instead of going back to archive directly
        Navigate(PageNames.Archive);
    }

    private void PrepareMeetingFolder(string meetingName)
    {
        // 1. Format date: [24-05-20] - Name
        string datePrefix = DateTime.Now.ToString("yy-MM-dd");
        string folderName = $"[{datePrefix}] - {meetingName}";

        _currentMeetingFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Meeting Archive", folderName);

        // Create directory if not exists
        if (!Directory.Exists(_currentMeetingFolderPath))
            Directory.CreateDirectory(_currentMeetingFolderPath);
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        if (IsPaused) _transcriptionService.Stop(); // Pause logic can be refined later
        else _transcriptionService.Start();
    }


     // -- ══════ Constructor  ══════ --//
    public MainWindowViewModel()
    {
        _currentPage = PageList.startPage;
    }
}