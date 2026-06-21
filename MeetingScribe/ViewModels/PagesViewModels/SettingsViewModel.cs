using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.UILogic;
using MeetingScribe.UILogic.ManifestReaders;
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
    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private ObservableCollection<ModelManifest> _availableModels = new();
    [ObservableProperty] private ModelManifest? _selectedModelItem;
    [ObservableProperty] private ModelManifest? _selectedModelAccItem;

    public SettingsViewModel()
    {
        LoadManifests();
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
            System.Diagnostics.Debug.WriteLine($"JSON Load Error: {ex.Message}");
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
}