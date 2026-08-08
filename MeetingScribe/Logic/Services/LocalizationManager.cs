using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.Enums;

namespace MeetingScribe.Logic.Services;

public partial class LocalizationManager : ObservableObject
{
    public static LocalizationManager Instance { get; } = new();

    private Dictionary<string, string> _currentStrings = new();
    [ObservableProperty] private string _currentLanguage = "en";

    // List of Available Languages for the ComboBox (Code and Name)
    public List<LanguageInfo> AvailableLanguages { get; private set; } = new();

    private LocalizationManager()
    {
        DiscoverLanguages();
        LoadLanguage("en"); // Default Language
    }

    // C# indexer: loc[“Key”]
    public string this[string key]
    {
        get
        {
            if (_currentStrings.TryGetValue(key, out var value)) return value;
            return $"***[{key}]***"; // If the key does not exist, it will return its name in parentheses
        }
    }

    private void DiscoverLanguages()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "I18n");
        if (!Directory.Exists(path))
        {
            LogService.Instance.LogError($"Localization directory not found: {path}");
            return;
        }
           
        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            var content = File.ReadAllText(file);
            var doc = JsonDocument.Parse(content);
            string name = doc.RootElement.GetProperty("__LanguageName").GetString() ?? Path.GetFileNameWithoutExtension(file);
            AvailableLanguages.Add(new LanguageInfo(Path.GetFileNameWithoutExtension(file), name));
        }
    }

    public void LoadLanguage(string langCode)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "I18n", $"{langCode}.json");
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        _currentStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        CurrentLanguage = langCode;

        // We are notifying the entire UI that the indexer has changed
        OnPropertyChanged("Item");
    }
}