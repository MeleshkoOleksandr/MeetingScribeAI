using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Enums;
using MeetingScribe.Logic;
using MeetingScribe.Logic.AI;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;
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

    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _editModeText = "Edit Summary";

    [ObservableProperty] private TranscriptLine? _selectedTranscriptLine;
    [ObservableProperty] private Participant? _selectedParticipantForLine;

    public ReviewMeetingViewModel(MeetingSession session, TranscriptionService transcriptionService, string whisperPath, AppSettings settings, MainWindowViewModel mainVm, Action<ReviewMeetingViewModel>? onCloseRequest = null)
    {
        _session = session;
        _whisperPath = whisperPath;
        _transcriptionService = transcriptionService;
        _onCloseRequest = onCloseRequest;
        Settings = settings;

        //Select the meeting summary to show on UI
        if (Session.TemplateSummary != null) SelectedSummaryTab = 1;
        if (Session.GeneralSummary != null) SelectedSummaryTab = 0;

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

            LogService.Instance.LogInfo($"Session data saved to {jsonPath}.");
            await LuminaMessageBox.Show("Success", "All changes have been saved to the archive.", LuminaMessageBoxType.Message);
        }
        catch (Exception ex)
        {
            LogService.Instance.LogError($"Failed to save session data: {ex.Message}");
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
            LogService.Instance.LogInfo("Transcription refinement completed successfully.");
        }
        catch (Exception ex)
        {
            LogService.Instance.LogError($"Error during transcription refinement: {ex.Message}");
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
        LogService.Instance.LogInfo("User requested cancellation of the current operation.");
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

            //UI Flags
            IsProcessing = true;
            IsIndeterminate = true; // Enable indeterminate progress bar since we don't have a specific progress metric for this operation
            IsAIRef = true;
            _mainVm.IsGlobalBusy = true;
            ProcessingProgress = 0;
            CurrentTaskName = "AI is analyzing conversation flow...";

            // We make participants list string for AI
            string participantsList = string.Join(", ", Session.Participants.Select(p => $"{p.Name} ({p.Alias})"));
            if (string.IsNullOrEmpty(participantsList))
                participantsList = "Auto-detect from context (names not provided)";

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
                var result = await aiService.ProcessChunkAsync(chunkText, participantsList, Session.Description, _processCts.Token);

                if (result != null)
                {
                    refinedTranscript.AddRange(result.Lines);
                    Session.SegmentSummaries.Add(result.SegmentSummary);
                }
                //If current task was cancelled, break the loop
                if (_processCts.IsCancellationRequested) { return; }
                //Make pause between chunks to avoid overwhelming the AI service
                await Task.Delay(1000);
                LogService.Instance.LogInfo($"Chunk {i + 1}/{chunks.Count} processed successfully.");
            }

            // Refresh the UI with the new speaker-labeled transcript
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Session.FullTranscript.Clear();
                foreach (var line in refinedTranscript) Session.FullTranscript.Add(line);

                Session.HasAIImprovements = true;
                IsDirty = true;
                CurrentTaskName = "Diarization & Chunk summaries complete!";
                LogService.Instance.LogInfo("Diarization and chunk summaries completed successfully.");
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

    private bool _isSyncingSelection; // Fuse Flag 
    // When we select a line, we clear the selected member in the combo box,
    // to prevent it from being accidentally renamed right away
    partial void OnSelectedTranscriptLineChanged(TranscriptLine? value)
    {
        if (value == null)
        {
            SelectedParticipantForLine = null;
            return;
        }
        // Enable sync mode (to prevent the renaming from taking effect)
        _isSyncingSelection = true;
       try
        {
            // Search the list of participants for the one whose name matches the name in the line
            SelectedParticipantForLine = Session.Participants.FirstOrDefault(p =>
                p.Name.Equals(value.SpeakerName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            // Turn off sync mode
            _isSyncingSelection = false;
        }
    }

    // When we select a person from the ComboBox
    partial void OnSelectedParticipantForLineChanged(Participant? value)
    {
        // CRITICAL : If this is just synchronization when clicking on a row, do nothing
        if (_isSyncingSelection) return;

        if (value == null || SelectedTranscriptLine == null) return;

        string oldName = SelectedTranscriptLine.SpeakerName;
        string newName = value.Name;

        // check to see if we've selected the same person who has already been appointed
        if (oldName == newName) return;

        // THE LOGIC BEHIND THE RENAMING 
        if (oldName.StartsWith("Speaker", StringComparison.OrdinalIgnoreCase))
        {
            // Batch Replacement for Speaker X
            foreach (var line in Session.FullTranscript)
            {
                if (line.SpeakerName == oldName) line.SpeakerName = newName;
            }
        }
        else
        {
            // Selective Replacement
            SelectedTranscriptLine.SpeakerName = newName;
        }

        IsDirty = true;
    }

    #endregion


    #region Sammary mode

    // 0 = General, 1 = Template
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSummaryText))]
    private int _selectedSummaryTab = 0;  //Summary tab selection index for UI binding

    // The property MarkdownViewer is binded to
    public string? CurrentSummaryText
    {
        get => SelectedSummaryTab == 0 ? Session.GeneralSummary : Session.TemplateSummary;
        set
        {
            //  The setter determines where to write the data
            if (SelectedSummaryTab == 0)
            {
                Session.GeneralSummary = value;
            }
            else
            {
                Session.TemplateSummary = value;
            }
            // Please be advised that the value has changed
            OnPropertyChanged(nameof(CurrentSummaryText));
            // We mark the session as “modified” so that the save logic works
            IsDirty = true;
        }
    }

    private enum SummaryTypes
    {
        GeneralSummary,
        TemplateSammary
    }

    [RelayCommand]
    private async Task GenerateFinalSummary()
    {
        var result = await makeSammary(SummaryTypes.GeneralSummary);
        if (result != null)
        {
            Session.GeneralSummary = result;
            SelectedSummaryTab = 0;
            RefreshSummaryUI();
            OnSelectedSummaryTabChanged(0);
            LogService.Instance.LogInfo("Final summary generated successfully.");
        }
    }

    [RelayCommand]
    private async Task GenerateTemplateSummary()
    {
        var result = await makeSammary(SummaryTypes.TemplateSammary);
        if (result != null)
        {
            Session.TemplateSummary = result;
            SelectedSummaryTab = 1;
            RefreshSummaryUI();
            OnSelectedSummaryTabChanged(0);
            LogService.Instance.LogInfo("Template summary generated successfully.");
        }
    }

    [RelayCommand]
    private async Task SaveSummary()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        if (SelectedSummaryTab == 0)
        {
            MeetingSummarySaver.SaveGeneralSummaryAsync(Session.GeneralSummary, Session.Name, Session.StartTime.ToString("dd.MM.yyyy"), desktop.MainWindow);
        }
        else
        {
            var (present, absent) = ParticipantHelper.GetFormattedParticipantLists(Session);
            MeetingSummarySaver.SaveTemplateSummaryAsync(Session.TemplateSummary, Session.StartTime.ToString("dd.MM.yyyy"), present, absent, Session.MeetingTopics, desktop.MainWindow);
        }
    }

    private void RefreshSummaryUI()
    {
        OnPropertyChanged(nameof(CurrentSummaryText));
        OnPropertyChanged(nameof(Session.ShowSummaryTabs));
        IsDirty = true;
    }

    partial void OnSelectedSummaryTabChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentSummaryText));
        // Workaround: Sometimes crashes due to a DynamicResource race (Color → IBrush).
        // We force a re-render during the next dispatcher cycle.
        Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(CurrentSummaryText));
        }, DispatcherPriority.Background);
    }

    private async Task<string?> makeSammary(SummaryTypes sammaryType)
    {
        string result = "";

        await ConnectAiService();
        if (aiService == null) return null;

        // Check if we have any segment summaries to work with
        if (!Session.HasAIImprovements || Session.SegmentSummaries.Count == 0)
        {
            var res = await LuminaMessageBox.Show("Step Missing",
                "Please run 'Diarization and Refinement' first to prepare data for summary.",
                LuminaMessageBoxType.Message);
            return null;
        }

        try
        {
            IsProcessing = true;
            IsIndeterminate = true;
            CurrentTaskName = "Synthesizing final meeting protocol...";

            // Sendind the segment summaries to the AI service for final summary generation
            switch (sammaryType)
            {
                case SummaryTypes.GeneralSummary:
                    result = await aiService.StitchSummariesAsync(Session.SegmentSummaries, Session.Description, Session.Language);
                    break;
                case SummaryTypes.TemplateSammary:
                    result = await aiService.TemplateSummariesAsync(Session.SegmentSummaries, Session.Description, Session.Language);
                    break;
                default:
                    break;
            }

            if (!string.IsNullOrEmpty(result))
            {
                IsTranscriptionView = false;
            }
            else
            {
                return null;
            }
        }
        finally { IsProcessing = false; }

        return result;
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        EditModeText = "Stop Edit";
        _mainVm.IsGlobalBusy = true;
        IsEditMode = !IsEditMode;

        if (!IsEditMode)
        {
            OnSelectedSummaryTabChanged(0);
            _mainVm.IsGlobalBusy = false;
            EditModeText = "Edit Summary";
        }
    }

    #endregion

}