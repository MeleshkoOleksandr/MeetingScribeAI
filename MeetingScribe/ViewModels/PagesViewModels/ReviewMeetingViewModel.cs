using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using System;
using System.Collections.Generic;
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


    // Второе свойство для второй кнопки
    public bool IsSummaryView
    {
        get => !IsTranscriptionView;
        set => IsTranscriptionView = !value;
    }

    // Когда меняется IsTranscriptionView, нужно уведомить UI об изменении IsSummaryView
    partial void OnIsTranscriptionViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSummaryView));
    }

    public ReviewMeetingViewModel(MeetingSession session)
    {
        _session = session;
    }

    // --- Improve recognition (Offline Whisper) ---
    [RelayCommand]
    private async Task ImproveRecognition()
    {
        IsProcessing = true;
        CurrentTaskName = "Refining transcription...";
        ProcessingProgress = 0;

        //Start task
        await Task.Delay(2000); 

        IsProcessing = false;
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
}
