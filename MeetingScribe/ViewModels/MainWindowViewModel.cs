using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;
using MeetingScribe.UILogic.Enums;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace MeetingScribe.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //  -- ══════ Fields & Properties  ══════ --//
    #region Fields & Properties

    // Link to settings (from the PageList object)
    public AppSettings CurrentSettings => ((SettingsViewModel)PageList.GetByTarget(PageNames.Settings).Page).Settings;

    // ---   Navigation state 
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

    // ---   Audio recording and speech recognition state
    private readonly TranscriptionService _transcriptionService = new();
    private readonly MeetingManager _meetingManager;
    private MeetingSession _currentSession = new MeetingSession();

    private DispatcherTimer? _timer;
    private DateTime _startTime;
  
    [ObservableProperty] private string _elapsedTime = "00:00:00";
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isPaused;

    // Current volume level (0–100)
    [ObservableProperty] private double _volumeLevel;
    // Data to illustrate the  volume level history (for the waveform visualization)
    public ObservableCollection<double> WaveformHistory { get; } = new();

    // Flag to indicate if the user has started heavy and long tasks (like transcription or AI processing)
    [ObservableProperty] private bool _isGlobalBusy;

    // Flag to indicate if there are unread critical errors in the log
    [ObservableProperty] private bool _hasUnreadErrors;

    #endregion


    // -- ══════ Navigation  ══════ --//
    #region Navigation

    private void HandleNavigation(NavigationItem? value)
    {
        if (value == null) return;

        // If we go to the archive, we update the list of files
        if (value.Page is ArchiveViewModel archiveVm)
        {
            archiveVm.LoadArchive();
        }

        // IF THE USER NAVIGATES TO THE "NEW MEETING" PAGE
        if (value.Page is NewMeetingViewModel newMeetingVm)
        {
            newMeetingVm.RefreshParticipantsFromBase();
        }

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

    // This method is called from the Archive page when the user wants to open a past meeting for review
    public void OpenMeetingReview(MeetingSession session)
    {
        //  Create a new ReviewMeetingViewModel with the selected session and navigate to it
        string whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "WhisperModels", CurrentSettings.SelectedAccModel);
        var reviewVm = new ReviewMeetingViewModel(session, _transcriptionService, whisperPath, CurrentSettings, this, CloseMeetingReview);

        // Creating a  navigation item for this review pag
        var reviewNavItem = new NavigationItem
        {
            Label = session.Name,
            Icon = "NotebookOutline",
            Target = PageNames.Review,
            Page = reviewVm
        };

        // Add to the dynamic sidebar list and navigate to it
        PageList.AddTemporaryItem(reviewNavItem);
        Navigate(reviewNavItem);
    }

    //  This method is called from the ReviewMeetingViewModel when the user wants to close the review and return to the archive
    private void CloseMeetingReview(ReviewMeetingViewModel vm)
    {
        // Search for the navigation item associated with this review page in the temporary items list
        var itemToRemove = PageList.TemporaryItems.FirstOrDefault(i => i.Page == vm);

        if (itemToRemove != null)
        {
            PageList.RemoveTemporaryItem(itemToRemove);
            // Return to the Archive page after closing the review
            Navigate(PageNames.Archive);
        }
    }

    [RelayCommand]
    private void OpenLogs()
    {
        HasUnreadErrors = false;
        Navigate(PageNames.Logs);
    }

    #endregion


    // -- ══════ Meetings Recording functions  ══════ --//
    #region Meetings Recording functions

    // Command triggered by the "Start Recording" button in NewMeetingView
    [RelayCommand]
    private async Task StartMeeting()
    {
        // Check if we are currently on the New Meeting setup page
        if (CurrentPage is not NewMeetingViewModel setupPage) return;

        try
        {
            // Initialization via the manager
            _currentSession = await _meetingManager.InitializeMeeting(setupPage.GetSessionData(), CurrentSettings);
            // Reset the NewMeetingViewModel state for next session
            setupPage.ResetForm();

            // Create the Live Page (ActiveMeetingViewModel)
            var activeVm = new ActiveMeetingViewModel(_currentSession.Name, _currentSession.Language);
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

            // Start
            _transcriptionService.TranscriptionUpdated -= OnNewTextReceived; // Clean up old subs
            _transcriptionService.TranscriptionUpdated += OnNewTextReceived;
            _meetingManager.Start();

            // Start the Elapsed Time timer
            _startTime = DateTime.Now;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;
                ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
            };
            _timer.Start();

            //Show the recording state in the UI
            IsRecording = true;
            IsPaused = false;

            LogService.Instance.LogInfo($"Meeting '{_currentSession.Name}' started at {_startTime}.");
        }
        catch (Exception ex)
        {
            // Log or show error (e.g., if models are missing or GPU failed)
            LogService.Instance.LogError($"Error starting meeting: {ex.Message}");
        }
    }

    // This method is called every time Whisper finishes transcribing a speech segment.
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
        _timer?.Stop();
        _meetingManager.Stop();

        // Finalize the session data and save it as JSON in the meeting folder
        if (CurrentPage is ActiveMeetingViewModel activeVm)
        {
            _meetingManager.SaveSession(DateTime.Now - _startTime, activeVm.TranscriptLines);
        }

        // Clean up UI
        var recordingItem = PageList.GetByTarget(PageNames.Recording);
        if (recordingItem != null) PageList.RemoveTemporaryItem(recordingItem);

        // Create an overview page and pass our session to it
        string whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "WhisperModels", CurrentSettings.SelectedAccModel);
        var reviewPage = new NavigationItem
        {
            Label = "Review:",
            Description = _currentSession?.Name,
            Icon = "NotebookOutline",
            Target = PageNames.Review,
            Page = new ReviewMeetingViewModel(_currentSession, _transcriptionService,  whisperPath, CurrentSettings , this, CloseMeetingReview)
        };

        PageList.AddTemporaryItem(reviewPage);
        Navigate(reviewPage);
        LogService.Instance.LogInfo($"Meeting '{_currentSession.Name}' stopped and saved.");
    }

    [RelayCommand]
    private void TogglePause()
    {
        //TODO: Implement proper pause/resume logic in the TranscriptionService. For now, we just toggle the state and stop/start the service.
        IsPaused = !IsPaused;
        if (IsPaused) _transcriptionService.Stop(); // Pause logic can be refined later
        else _transcriptionService.Start();

        LogService.Instance.LogInfo($"Meeting '{_currentSession.Name}' {(IsPaused ? "paused" : "resumed")}.");
    }

    [RelayCommand]
    private async Task LoadMeetingFromFile()
    {
        // Check if we are currently on the New Meeting setup page
        if (CurrentPage is not NewMeetingViewModel setupPage) return;

        //  Get Storage Provider from the App
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var storage = desktop.MainWindow?.StorageProvider;
        if (storage == null) return;

        //  Defining a filter for audio files
        var audioFilter = new FilePickerFileType("Audio Files")
        {
            Patterns = new[] { "*.mp3", "*.wav", "*.m4a", "*.wma", "*.flac" },
            MimeTypes = new[] { "audio/*" }
        };

        //  Open the file selection dialog
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Audio File",
            FileTypeFilter = new[] { audioFilter },
            AllowMultiple = false
        });

        if (result == null || result.Count == 0) return;

        //  We get the local path to the file (Avalonia returns a URI)
        string selectedPath = result[0].Path.LocalPath;

        // Create Session (Transcoding happens here)
        var session = await _meetingManager.CreateSessionFromAudioFile(selectedPath, setupPage.GetSessionData(), CurrentSettings);
        setupPage.ResetForm();

        //  Navigate to Review
        string whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "WhisperModels", CurrentSettings.SelectedAccModel);

        var reviewVm = new ReviewMeetingViewModel(session, _transcriptionService, whisperPath, CurrentSettings , this, CloseMeetingReview);
        var reviewPage = new NavigationItem
        {
            Label = "Review: " + session.Name,
            Icon = "FileMusicOutline",
            Target = PageNames.Review,
            Page = reviewVm
        };

        PageList.AddTemporaryItem(reviewPage);
        Navigate(reviewPage);

        //  Trigger auto-processing
        _ = reviewVm.ImproveRecognitionCommand.ExecuteAsync(null);
        LogService.Instance.LogInfo($"Loaded meeting from file '{selectedPath}' and started review.");
    }

    private void SetupTranscriptionStreaming()
    {
        _transcriptionService.VolumeLevelChanged += (level) =>
        {
            // Updating the microphone scale
            VolumeLevel = level * 100;
            // Update the wave (add a new value to the history)
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                WaveformHistory.Add(level);
                if (WaveformHistory.Count > 50) WaveformHistory.RemoveAt(0);
            });
        };
    }

    #endregion


    // -- ══════ Constructor  ══════ --//
    public MainWindowViewModel()
    {
        Navigate(PageNames.New);
        SetupTranscriptionStreaming();

        LogService.Instance.OnCriticalError += () => HasUnreadErrors = true;

        // Initialize the MeetingManager with the transcription service
        _meetingManager = new MeetingManager(_transcriptionService);
        // Initialize the Archive page with the callback to open a meeting review
        (PageList.GetByTarget(PageNames.Archive).Page as ArchiveViewModel).InitArchiveViewModel(OpenMeetingReview);

        LogService.Instance.LogInfo("App initialized.");
    }
}