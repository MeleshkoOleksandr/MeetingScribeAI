using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeetingScribe.ViewModels;

public partial class ReviewMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private MeetingSession _session;
    [ObservableProperty] private bool _isTranscriptionView = true;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _processingProgress;
    [ObservableProperty] private string _currentTaskName = "";

    TranscriptionService _transcriptionService;
    string _whisperPath;

    // The second property for the tab buttons
    public bool IsSummaryView
    {
        get => !IsTranscriptionView;
        set => IsTranscriptionView = !value;
    }

    // When IsTranscriptionView changes, the UI must be notified of the change in IsSummaryView
    partial void OnIsTranscriptionViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSummaryView));
    }

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
            CurrentTaskName = "Initializing High-Quality AI...";
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

            await Task.Delay(2000);
            IsProcessing = false;
        }
    }
}
