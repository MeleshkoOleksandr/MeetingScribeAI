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

    public SettingsViewModel()
    {
        LoadManifests();
    }

    private void LoadManifests()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "models_manifest.json");

            if (!File.Exists(path))
            {
                // Если файла нет, создаем дефолтный список, чтобы UI не был пустым
                AvailableModels = new ObservableCollection<ModelManifest> {
                new ModelManifest { Name = "Default (No manifest found)", FileName = "default.bin" }
            };
                return;
            }

            var json = File.ReadAllText(path);
            var models = JsonSerializer.Deserialize<List<ModelManifest>>(json);

            if (models != null)
            {
                AvailableModels = new ObservableCollection<ModelManifest>(models);

                // 2. Важно: Ищем модель в списке, сравнивая FileName
                // Мы должны найти ТОТ ЖЕ объект, который лежит в коллекции AvailableModels
                SelectedModelItem = AvailableModels.FirstOrDefault(m => m.FileName == Settings.SelectedModel)
                                   ?? AvailableModels.FirstOrDefault();
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
}