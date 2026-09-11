using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Enums;
using MeetingScribe.Logic;
using MeetingScribe.Logic.AI;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic;
using MeetingScribe.UILogic.ManifestReaders;
using MeetingScribe.ViewModels.Windows;
using MeetingScribe.Views;
using MeetingScribe.Views.Windows;
using MeetingScribe.Views.Controls;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MeetingScribe.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    // --- Observables ---
    [ObservableProperty] private AppSettings _settings;
    [ObservableProperty] private ObservableCollection<ModelManifest> _availableModels = new();
    [ObservableProperty] private ModelManifest? _selectedModelItem;
    [ObservableProperty] private ModelManifest? _selectedModelAccItem;

    // Provider list from ai_providers.json
    [ObservableProperty] private ObservableCollection<AiProvider> _aiProviders = new();
    // Selected provider from the list, which is saved in settings
    [ObservableProperty] private AiProvider? _selectedAiProvider;
    // Current API key for the selected provider, loaded from SecretsManager
    [ObservableProperty] private string _currentApiKey = "";

    // UI localization
    public List<LanguageInfo> Languages => LocalizationManager.Instance.AvailableLanguages;
    [ObservableProperty] private LanguageInfo? _selectedUiLanguage;

    public SettingsViewModel()
    {
        _settings = AppSettings.Load();
        _settings.PropertyChanged += OnSettingsChanged;

        LoadManifests();
        LoadAiProviders();

        LocalizationManager.Instance.LoadLanguage(_settings.UiLanguage);
        SelectedUiLanguage = Languages.FirstOrDefault(l => l.Code == LocalizationManager.Instance.CurrentLanguage);
    }

    private void LoadManifests()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "models_manifest.json");

            if (!File.Exists(path)) return;
           
            var json = File.ReadAllText(path);
            var models = JsonSerializer.Deserialize<List<ModelManifest>>(json);

            if (models != null)
            {
                AvailableModels = new ObservableCollection<ModelManifest>(models);
                // Search for a model in the list by comparing FileName
                SelectedModelItem = AvailableModels.FirstOrDefault(m => m.FileName == Settings.SelectedModel) ?? AvailableModels.FirstOrDefault();
                SelectedModelAccItem = AvailableModels.FirstOrDefault(m => m.FileName == Settings.SelectedAccModel) ?? AvailableModels.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.LogError($"Failed to load models_manifest.json: {ex.Message}");
        }
    }

    // Update settings when model changes
    partial void OnSelectedModelItemChanged(ModelManifest? value)
    {
        if (value != null) Settings.SelectedModel = value.FileName;
    }

    partial void OnSelectedModelAccItemChanged(ModelManifest? value)
    {
        if (value != null) Settings.SelectedAccModel = value.FileName;
    }


    private void LoadAiProviders()
    {
        // Load the list of AI providers from ai_providers.json
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "ai_providers.json");
        var providers = JsonSerializer.Deserialize<List<AiProvider>>(File.ReadAllText(path));
        AiProviders = new ObservableCollection<AiProvider>(providers ?? new());

        // Set the selected provider based on the saved settings, or default to the first provider if not found
        SelectedAiProvider = AiProviders.FirstOrDefault(p => p.Id == Settings.AiProviderId) ?? AiProviders.FirstOrDefault();
    }

    // Update settings and load API key when provider changes
    partial void OnSelectedAiProviderChanged(AiProvider? value)
    {
        if (value == null) return;

        Settings.AiProviderId = value.Id;

        // Auto-load the API key for the selected provider from SecretsManager
        var keys = SecretsManager.LoadKeys();
        CurrentApiKey = keys.TryGetValue(value.Id, out var key) ? key : "";
    }

    // Save the API key when it changes in the text field
    partial void OnCurrentApiKeyChanged(string value)
    {
        if (SelectedAiProvider != null)
        {
            SecretsManager.SaveKey(SelectedAiProvider.Id, value);
        }
    }

    // Toggle the visibility of the API key in the UI
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar))]
    private bool _isApiKeyVisible;
    public char PasswordChar => IsApiKeyVisible ? '\0' : '*';

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsApiKeyVisible = !IsApiKeyVisible;
    }

    partial void OnSelectedUiLanguageChanged(LanguageInfo? value)
    {
        if (value != null)
        {
            LocalizationManager.Instance.LoadLanguage(value.Code);
            Settings.UiLanguage = value.Code;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task EditPrompts()
    {
        var dialog = new PromptsEditorWindow { DataContext = new PromptsEditorViewModel() };
        var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _settings.Save();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RestoreDefaults()
    {
        var result = await LuminaMessageBox.Show(
            Loc("view_Settings_RestoreTitle"),
            Loc("view_Settings_RestoreMsg"),
            LuminaMessageBoxType.Danger,
            Loc("view_Settings_RestoreConfirm"));

        if (result == LuminaMessageBox.MessageBoxResult.Confirm)
        {
            Settings.PropertyChanged -= OnSettingsChanged;
            
            Settings = new AppSettings();
            Settings.Save();
            
            Settings.PropertyChanged += OnSettingsChanged;

            // Reapply selections based on new defaults
            SelectedModelItem = AvailableModels.FirstOrDefault(m => m.FileName == Settings.SelectedModel) ?? AvailableModels.FirstOrDefault();
            SelectedModelAccItem = AvailableModels.FirstOrDefault(m => m.FileName == Settings.SelectedAccModel) ?? AvailableModels.FirstOrDefault();
            SelectedAiProvider = AiProviders.FirstOrDefault(p => p.Id == Settings.AiProviderId) ?? AiProviders.FirstOrDefault();
            LocalizationManager.Instance.LoadLanguage(Settings.UiLanguage);
            SelectedUiLanguage = Languages.FirstOrDefault(l => l.Code == LocalizationManager.Instance.CurrentLanguage);
        }
    }
}