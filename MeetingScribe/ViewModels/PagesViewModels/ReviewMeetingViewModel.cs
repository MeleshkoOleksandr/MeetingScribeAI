using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic.Enums;

using System;
using System.IO;
using System.Linq;
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

    // comands for UI buttons
    [RelayCommand] private void CloseReview() { /* Логика закрытия вкладки */ }
    [RelayCommand] private void SaveChanges() { /* Логика записи JSON на диск */ }

    #endregion


    public ReviewMeetingViewModel(MeetingSession session, TranscriptionService transcriptionService, string whisperPath)
    {
        _session = session;
        _whisperPath = whisperPath;
        _transcriptionService = transcriptionService;
    }

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

    // Текст Саммари
    [ObservableProperty] private string _summaryMarkdown = "# Meeting Summary\nImported results will appear here...";
}