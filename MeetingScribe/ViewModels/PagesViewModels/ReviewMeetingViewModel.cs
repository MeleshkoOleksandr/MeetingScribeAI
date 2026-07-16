using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using MeetingScribe.Logic;
using MeetingScribe.Logic.AI;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;
using MeetingScribe.UILogic.Enums;
using MeetingScribe.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
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

    AppSettings Settings;
    [ObservableProperty] private bool _isDirty; // Unsaved changes flag
    private readonly Action<ReviewMeetingViewModel>? _onCloseRequest;

    IAiService? aiService;
    private readonly MainWindowViewModel _mainVm;

    private CancellationTokenSource? _processCts;
    public bool IsGlobalBusy => _mainVm.IsGlobalBusy;
    [ObservableProperty] private bool _isImprovingText; // Flag to indicate if text improvement is in progress
    [ObservableProperty] private bool _isAIRef; // Flag to indicate ai refinement is in progress

    public ReviewMeetingViewModel(MeetingSession session, TranscriptionService transcriptionService, string whisperPath, AppSettings settings, MainWindowViewModel mainVm, Action<ReviewMeetingViewModel>? onCloseRequest = null)
    {
        _session = session;
        _whisperPath = whisperPath;
        _transcriptionService = transcriptionService;
        _onCloseRequest = onCloseRequest;
        Settings = settings;

        _mainVm = mainVm;
        // Propertie that block buttons when long-running operations are in progress
        _mainVm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(MainWindowViewModel.IsGlobalBusy)) { OnPropertyChanged(nameof(IsGlobalBusy)); } };

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

    private async Task ConnectAiService()
    {
        if (aiService == null)
        {
            // Getting AI prvider
            var providersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "ai_providers.json");
            var providers = JsonSerializer.Deserialize<List<AiProvider>>(File.ReadAllText(providersPath));
            var config = providers?.FirstOrDefault(p => p.Id == Settings.AiProviderId);

            if (config == null) throw new Exception("AI Provider configuration not found.");

            // Getting AI Api key
            var keys = SecretsManager.LoadKeys();
            if (!keys.TryGetValue(config.Id, out var apiKey) || string.IsNullOrEmpty(apiKey))
            {
                await LuminaMessageBox.Show("Key Missing", $"Please enter an API key for {config.Name} in Settings.", LuminaMessageBoxType.Danger);
                return;
            }

            // Creating the AI service instance
            aiService = AiServiceFactory.Create(config, apiKey);
        }
    }

    #endregion


    #region Script mode

    // --- Improve recognition (Offline Whisper) ---
    [RelayCommand]
    private async Task ImproveRecognition()
    {
        if (IsProcessing) return;

        try
        {
            _processCts = new CancellationTokenSource();

            IsProcessing = true;
            IsIndeterminate = true;
            IsImprovingText = true;
            _mainVm.IsGlobalBusy = true;
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

            var refinedLines = await _transcriptionService.ProcessFileAsync(audioPath, progressHandler, _processCts.Token);

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
            _transcriptionService.Stop();
            _transcriptionService.UnloadModel();

            IsProcessing = false;
            IsIndeterminate = false;
            IsImprovingText = false;
            _mainVm.IsGlobalBusy = false;

            _processCts?.Dispose();
            _processCts = null;
        }
    }

    [RelayCommand]
    private void CancelCurrentAction()
    {
        _processCts?.Cancel();
        CurrentTaskName = "Cancelling current operation...";

        IsImprovingText = false;
        IsAIRef = false;
        _mainVm.IsGlobalBusy = false;
    }

    // --- Appoint the speakers (Semantic Diarization) ---
    [RelayCommand]
    private async Task SplitBySpeakers()
    {
        // Check if processing is already in progress
        if (IsProcessing || Session.FullTranscript.Count == 0) return;

        try
        {
            await ConnectAiService();
            _processCts = new CancellationTokenSource();

            IsProcessing = true;
            IsIndeterminate = true; // Enable indeterminate progress bar since we don't have a specific progress metric for this operation
            IsAIRef = true;
            _mainVm.IsGlobalBusy = true;
            ProcessingProgress = 0;
            CurrentTaskName = "AI is analyzing conversation flow...";

            // Making chanks of the transcript to send to the AI service
            string rawTextFull = string.Join("\n", Session.FullTranscript.Select(t => $"{t.Timestamp} {t.Text}"));
            var lines = rawTextFull.Split('\n');
            var chunks = TextOparations.GroupLinesByTime(lines, 15);

            var refinedTranscript = new List<TranscriptLine>();
            Session.SegmentSummaries.Clear();

            for (int i = 0; i < chunks.Count; i++)
            {
                IsIndeterminate = false;
                CurrentTaskName = $"AI Analysis: Part {i + 1} of {chunks.Count}...";
                ProcessingProgress = (double)i / chunks.Count * 100;

                string chunkText = string.Join("\n", chunks[i]);

                // AI service call to analyze the chunk and return speaker-labeled lines
                var result = await aiService.ProcessChunkAsync(chunkText, "Auto-detect", Session.Description, _processCts.Token);

                if (result != null)
                {
                    refinedTranscript.AddRange(result.Lines);
                    Session.SegmentSummaries.Add(result.SegmentSummary);
                }
                //If current task was cancelled, break the loop
                if (_processCts.IsCancellationRequested) { return;}
                //Make pause between chunks to avoid overwhelming the AI service
                await Task.Delay(1000);
            }

            // Refresh the UI with the new speaker-labeled transcript
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Session.FullTranscript.Clear();
                foreach (var line in refinedTranscript) Session.FullTranscript.Add(line);

                Session.HasAIImprovements = true;
                IsDirty = true;
                CurrentTaskName = "Diarization & Chunk summaries complete!";
            });
        }
        finally 
        { 
            IsProcessing = false;
            IsIndeterminate = false;
            IsAIRef = false;
            _mainVm.IsGlobalBusy = false;

            _processCts?.Dispose();
            _processCts = null;
        }
    }

    #endregion


    #region Sammary mode

    private enum SammaryTypes
    {
        GeneralSummary,
        TemplateSammary
    }

    [RelayCommand]
    private async Task GenerateFinalSummary()
    {
        await makeSammary(SammaryTypes.GeneralSummary);
      
    }

    [RelayCommand]
    private async Task GenerateTemplateSummary()
    {
        await makeSammary(SammaryTypes.TemplateSammary);
    }

    private async Task<bool> makeSammary(SammaryTypes sammaryType)
    {
        await ConnectAiService();

        // Check if we have any segment summaries to work with
        if (!Session.HasAIImprovements || Session.SegmentSummaries.Count == 0)
        {
            var res = await LuminaMessageBox.Show("Step Missing",
                "Please run 'Diarization and Refinement' first to prepare data for summary.",
                LuminaMessageBoxType.Message);
            return false;
        }

        try
        {
            IsProcessing = true;
            IsIndeterminate = true;
            CurrentTaskName = "Synthesizing final meeting protocol...";

            // Sendind the segment summaries to the AI service for final summary generation
            string finalMarkdown = "";
            switch (sammaryType)
            {
                case SammaryTypes.GeneralSummary:
                    finalMarkdown = await aiService.StitchSummariesAsync(Session.SegmentSummaries, Session.Description);
                    break;
                case SammaryTypes.TemplateSammary:
                    finalMarkdown = await aiService.TemplateSummariesAsync(Session.SegmentSummaries, Session.Description);
                    break;
                default:
                    break;
            }
      
            if (!string.IsNullOrEmpty(finalMarkdown))
            {
                Session.GeneralSummary = finalMarkdown;
                Session.HasSummary = true;
                IsTranscriptionView = false;
            }
        }
        finally { IsProcessing = false; }

        return true;
    }

    #endregion

}