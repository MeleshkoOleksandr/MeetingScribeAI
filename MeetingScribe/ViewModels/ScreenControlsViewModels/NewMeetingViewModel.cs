using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.UILogic.ManifestReaders;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MeetingScribe.ViewModels;

public partial class NewMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<LanguageManifest> _languages = new();
    [ObservableProperty] private LanguageManifest? _selectedLanguage;
    [ObservableProperty] private string _meetingName = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _meetingTopics = "";

    public NewMeetingViewModel()
    {
        // Default name with date
        MeetingName = DateTime.Now.ToString("yy-MM-dd-HH-mm") + " - New Meeting";
        LoadLanguages();
    }

    private void LoadLanguages()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "languages_manifest.json");
        if (File.Exists(path))
        {
            var data = JsonSerializer.Deserialize<List<LanguageManifest>>(File.ReadAllText(path));
            Languages = new ObservableCollection<LanguageManifest>(data ?? new());
            if (Languages.Any()) SelectedLanguage = Languages.First(); // Select the first language by default
        }
    }

    public MeetingSession GetSessionData() => new()
    {
        Name = MeetingName,
        Description = Description,
        MeetingTopics = MeetingTopics,
        Language = SelectedLanguage?.Code ?? "auto" // Save the CODE (ru, en)
    };
}