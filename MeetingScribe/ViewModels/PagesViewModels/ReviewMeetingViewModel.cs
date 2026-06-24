using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic.Enums;
using MeetingScribe.Views;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingScribe.ViewModels;

public partial class ReviewMeetingViewModel : ViewModelBase
{
    // Whisper path and transcription service are injected for processing
    string _whisperPath;
    TranscriptionService _transcriptionService;
    // The current meeting session being reviewed
    [ObservableProperty] private MeetingSession _session;
    // Properties for UI binding to show processing state and progress
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _processingProgress;
    [ObservableProperty] private string _currentTaskName = "";
    [ObservableProperty] private bool _isIndeterminate;

    [ObservableProperty] private bool _isDirty; // Unsaved changes flag
    private readonly Action<ReviewMeetingViewModel>? _onCloseRequest;

    public ReviewMeetingViewModel(MeetingSession session, TranscriptionService transcriptionService, string whisperPath, Action<ReviewMeetingViewModel>? onCloseRequest = null)
    {
        _session = session;
        _whisperPath = whisperPath;
        _transcriptionService = transcriptionService;
        _onCloseRequest = onCloseRequest;

        // Subscribe to property changes in the session to track unsaved changes
        Session.PropertyChanged += (s, e) => IsDirty = true;
        // Subscribe to collection changes in the transcript to track unsaved changes
        Session.FullTranscript.CollectionChanged += (s, e) => IsDirty = true;
        // initialize the dirty flag to false since we just loaded the session
        IsDirty = false;
    }


    #region UI Navigation and general commands

    [ObservableProperty] private ReviewMode _currentMode = ReviewMode.Script;

    // ToggleButtons properties for switching between different views in the UI
    public bool IsTranscriptionView
    {
        get => CurrentMode == ReviewMode.Script;
        set { if (value) CurrentMode = ReviewMode.Script; OnPropertyChanged(nameof(IsTranscriptionView)); OnPropertyChanged(nameof(IsSummaryView)); OnPropertyChanged(nameof(IsInfoView)); }
    }
    public bool IsSummaryView
    {
        get => CurrentMode == ReviewMode.Summary;
        set { if (value) CurrentMode = ReviewMode.Summary; OnPropertyChanged(nameof(IsTranscriptionView)); OnPropertyChanged(nameof(IsSummaryView)); OnPropertyChanged(nameof(IsInfoView)); }
    }
    public bool IsInfoView
    {
        get => CurrentMode == ReviewMode.Info;
        set { if (value) CurrentMode = ReviewMode.Info; OnPropertyChanged(nameof(IsTranscriptionView)); OnPropertyChanged(nameof(IsSummaryView)); OnPropertyChanged(nameof(IsInfoView)); }
    }

    
    // --- Save command ---
    [RelayCommand]
    private async Task SaveChanges()
    {
        try
        {
            string jsonPath = Path.Combine(Session.FolderPath, "session_data.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonContent = JsonSerializer.Serialize(Session, options);

            await File.WriteAllTextAsync(jsonPath, jsonContent);

            IsDirty = false; // Reset the dirty flag after saving

            await LuminaMessageBox.Show("Success", "All changes have been saved to the archive.", LuminaMessageBoxType.Message);
        }
        catch (Exception ex)
        {
            await LuminaMessageBox.Show("Error", $"Could not save changes: {ex.Message}", LuminaMessageBoxType.Danger);
        }
    }

    // --- Close command ---
    [RelayCommand]
    private async Task CloseReview()
    {
        if (IsDirty)
        {
            // If there are unsaved changes, prompt the user for confirmation before closing
            var result = await LuminaMessageBox.Show(
                "Unsaved Changes",
                "You have unsaved changes in this session. Are you sure you want to close it and lose your work?",
                LuminaMessageBoxType.Danger,
                "Discard Changes");

            if (result != LuminaMessageBox.MessageBoxResult.Confirm)
                return; // No confirmation, so we don't close the view
        }
        // Close the view and invoke the callback to notify the parent view model
        _onCloseRequest?.Invoke(this);
    }

    #endregion


    #region Script mode

    // --- Appoint the speakers (Semantic Diarization) ---
    [RelayCommand]
    private async Task SplitBySpeakers()
    {
        IsProcessing = true;
        CurrentTaskName = "AI Diarization...";

        // Send the text to Gemini and get the formatted result
        await Task.Delay(3000);

        IsProcessing = false;
    }

    // --- Improve recognition (Offline Whisper) ---
    [RelayCommand]
    private async Task ImproveRecognition()
    {
        if (IsProcessing) return;

        try
        {
            IsProcessing = true;
            IsIndeterminate = true;
            CurrentTaskName = "Initializing AI Model...";
            ProcessingProgress = 0;

            //  Gather existing text for the context prompt
            string oldText = string.Join(" ", Session.FullTranscript.Select(t => t.Text));

            //  Locate the audio file (we use the boosted one for better clarity)
            string audioPath = Path.Combine(Session.FolderPath, "boosted_record.wav");

            if (!File.Exists(audioPath))
            {
                CurrentTaskName = "Error: Audio file not found.";
                return;
            }

            // Initialize the service with the Quality Model and our Prompt
            // We use 'true' for useAllProcessor to finish faster
            await _transcriptionService.InitializeAsync(_whisperPath, Session.Language, oldText, true);

            // Start processing with progress reporting
            IsIndeterminate = false;
            CurrentTaskName = "Analyzing audio file...";
            var progressHandler = new Progress<double>(value => ProcessingProgress = value);

            var refinedLines = await _transcriptionService.ProcessFileAsync(audioPath, progressHandler);

            // Update the UI Collection
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Session.FullTranscript.Clear();
                foreach (var line in refinedLines)
                {
                    Session.FullTranscript.Add(line);
                }
            });

            CurrentTaskName = "Transcription successfully refined!";
            ProcessingProgress = 100;
        }
        catch (Exception ex)
        {
            CurrentTaskName = $"Failed: {ex.Message}";
        }
        finally
        {
            // Unload the heavy model to free up RAM/VRAM
            _transcriptionService.UnloadModel();

            IsProcessing = false;
            IsIndeterminate = false;
        }
    }

    #endregion


    #region Sammary mode

    // Текст Саммари
    [ObservableProperty] private string _summaryMarkdown = "# Meeting Summary\nImported results will appear here...";

    #endregion

}