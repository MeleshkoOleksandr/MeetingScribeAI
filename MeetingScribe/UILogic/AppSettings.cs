using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Text.Json;

namespace MeetingScribe.UILogic;

public partial class AppSettings : ObservableObject
{
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    // Speech recognition settings
    [ObservableProperty] private float _speechThreshold = 0.2f;
    [ObservableProperty] private int _silenceTimeoutMs = 600;
    [ObservableProperty] private float _audioGain = 3.0f;

    [ObservableProperty] private string _selectedModel = "ggml-small.bin";
    [ObservableProperty] private string _selectedAccModel = "ggml-large-v3-turbo.bin";
    [ObservableProperty] private string _transcriptionLanguage = "it";

    // AI provider settings
    [ObservableProperty] private string _aiProviderId = "Gemini";
    [ObservableProperty] private string _geminiApiKey = "";
    [ObservableProperty] private string _openAiApiKey = "";

    // UI Language
    [ObservableProperty] private string _uiLanguage = "en";

    public static AppSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
            catch (Exception) { /* Fallback to default */ }
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception) { /* Handle silently */ }
    }
}