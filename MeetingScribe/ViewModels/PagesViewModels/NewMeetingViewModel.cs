using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.UILogic.ManifestReaders;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


namespace MeetingScribe.ViewModels;

public partial class NewMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<LanguageManifest> _languages = new();
    [ObservableProperty] private LanguageManifest? _selectedLanguage;

    [ObservableProperty] private string _meetingName = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _meetingTopics = "";
    [ObservableProperty] private string _participantsText = "";

    public NewMeetingViewModel()
    {
        // Default name with date
        MeetingName = "New Meeting";
        LoadLanguages();
    }

    [RelayCommand]
    private void AddCurrentDate()
    {
        string datePrefix = $"[{DateTime.Now:yy-MM-dd}]";

        // If the meeting name already contains the date prefix, do not add it again
        if (!string.IsNullOrEmpty(MeetingName) && MeetingName.Contains(datePrefix))
            return;

        MeetingName = MeetingName + datePrefix;
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


    [RelayCommand]
    private async Task ImportAgenda()
    {
        // 1. Get the storage provider from the main window
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var storage = desktop.MainWindow?.StorageProvider;
        if (storage == null) return;

        // 2. Select the file using the file picker. Only allow .docx files
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Meeting Agenda",
            FileTypeFilter = new[] { new FilePickerFileType("Word Documents") { Patterns = new[] { "*.docx" } } },
            AllowMultiple = false
        });

        if (result.Count == 0) return;

        try
        {
            // 3. Exrcuting the parsing logic from AgendaParser
            string filePath = result[0].Path.LocalPath;
            var (participants, topics) = AgendaParser.ParseMeetingAgenda(filePath);

            // 4. Show the parsed data in the UI
            MeetingTopics = topics;
            ParticipantsText = participants;

            // Optional: If the description is empty, you can set a default description based on the agenda file name or other logic
            if (string.IsNullOrEmpty(Description))
                Description = $"Imported from agenda: {DateTime.Now:d}";
        }
        catch (Exception ex)
        {
            //  Parse error handling: Log the error and optionally show a message to the user
            System.Diagnostics.Debug.WriteLine($"Parsing error: {ex.Message}");
        }
    }
}