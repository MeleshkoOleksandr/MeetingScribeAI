using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;
using MeetingScribe.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    // -- ══════ Recording  ══════ --//

    // Command triggered by the "Start Recording" button in NewMeetingView
    [RelayCommand]
    private async Task StartMeeting()
    {
        // 1. Check if we are currently on the New Meeting setup page
        if (CurrentPage is not NewMeetingViewModel setupPage) return;

        try
        {
            // 2. Extract session data (Name, Description, Language) from the form
            _currentSession = setupPage.GetSessionData();

            // 3. Prepare AI Model Paths
            // Ensure these folders and files exist in your Output directory!
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string whisperPath = Path.Combine(baseDir, "Assets", "WhisperModels", "ggml-large-v3-turbo.bin");
            string vadPath = Path.Combine(baseDir, "Assets", "Models", "silero_vad.onnx");

            // 4. Initialize Whisper and VAD engines
            // This might take a few seconds depending on GPU/CPU speed
            await _transcriptionService.InitializeAsync(whisperPath, vadPath);

            // 5. Create the Live Page (ActiveMeetingViewModel)
            var activeVm = new ActiveMeetingViewModel(_currentSession.Name);

            // 6. Create a Temporary Navigation Item for the Sidebar
            var liveNavItem = new NavigationItem
            {
                Label = "Recording...",
                Icon = "Waveform",
                Target = PageNames.Recording,
                Page = activeVm,
                IsStartUp = false
            };

            // 7. Add to the dynamic sidebar list and navigate to it
            PageList.AddTemporaryItem(liveNavItem);
            Navigate(liveNavItem);

            // 8. Subscribe to transcription events
            // We use a separate method to handle incoming text segments
            _transcriptionService.TranscriptionUpdated -= OnNewTextReceived; // Clean up old subs
            _transcriptionService.TranscriptionUpdated += OnNewTextReceived;

            // 9. Start the Audio Engine and VAD loop
            _transcriptionService.Start();

            // 10. Start the Elapsed Time timer
            _startTime = DateTime.Now;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;
                ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
            };
            _timer.Start();

            // 11. Trigger UI state (Shows the Bottom Control Bar)
            IsRecording = true;
            IsPaused = false;
        }
        catch (Exception ex)
        {
            // Log or show error (e.g., if models are missing or GPU failed)
            // You can add a 'StatusText' property to show this on UI
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
        _transcriptionService.Stop();
        _timer?.Stop();
        IsRecording = false;

        if (_currentSession != null && CurrentPage is ActiveMeetingViewModel activeVm)
        {
            // Save the transcript to the session object
            _currentSession.FullTranscript = activeVm.TranscriptLines.ToList();
            _currentSession.Duration = DateTime.Now - _startTime;
        }

        // Clean up UI
        var recordingItem = PageList.GetByTarget(PageNames.Recording);
        if (recordingItem != null) PageList.RemoveTemporaryItem(recordingItem);

        Navigate(PageNames.Archive);
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

        //NavigationItem newRecording = new NavigationItem
        //{
        //    Label = "Meeting Recording",
        //    Icon = "Waveform",
        //    Target = PageNames.Recording,
        //    Description = "Meeting name",
        //    IsStartUp = false,
        //    Page = new ActiveMeetingViewModel()
        //};

        //PageList.AddTemporaryItem(newRecording);
        //Navigate(newRecording);

    }
}