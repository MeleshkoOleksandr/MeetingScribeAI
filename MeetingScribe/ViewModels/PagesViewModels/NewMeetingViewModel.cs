using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic.ManifestReaders;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private string _time = "08:00-08:45";

    // All avalable people in participants.json
    private List<Participant> _allParticipants = new();
    // People who take part in meeting
    public ObservableCollection<Participant> SelectedParticipants { get; } = new();
    // List of persons available to join the meeting
    public ObservableCollection<Participant> AvailableToJoin { get; } = new();

    // ComboBox coloctions
    public ObservableCollection<TeamGroup> AvailableTeams { get; } = new();
    public ObservableCollection<Venue> AvailableVenues { get; } = new();
    // Selected items on UI
    [ObservableProperty] private TeamGroup? _selectedTeam;
    [ObservableProperty] private Venue? _selectedVenue;

    public NewMeetingViewModel()
    {
        // Default name with date
        MeetingName = "New Meeting";
        LoadLanguages();
        RefreshParticipantsFromBase();
        LoadGroups();
        LoadVenues();
    }

    private void LoadVenues()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "Venue.json");
            if (File.Exists(path))
            {
                var venues = JsonSerializer.Deserialize<List<Venue>>(File.ReadAllText(path));
                AvailableVenues.Clear();
                if (venues != null) foreach (var v in venues) AvailableVenues.Add(v);
            }
        }
        catch { LogService.Instance.LogError("Couldn't load json file with Venue list"); }
    }

    private void LoadGroups()
    {
        var (_, groups) = TeamStorageService.LoadData();
        AvailableTeams.Clear();
        foreach (var g in groups) AvailableTeams.Add(g);
    }

    [RelayCommand]
    private void AddCurrentDate()
    {
        string datePrefix = $"{DateTime.Now:dd-MM-yyyy}";

        // If the meeting name already contains the date prefix, do not add it again
        if (!string.IsNullOrEmpty(MeetingName) && MeetingName.Contains(datePrefix))
            return;

        Date = datePrefix;
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
        Language = SelectedLanguage?.Code ?? "auto", // Save the CODE (ru, en)
        Participants = new ObservableCollection<Participant>(SelectedParticipants),

        Date = Date,
        Time = Time,
        Team = SelectedTeam?.Name ?? string.Empty,
        Venue = SelectedVenue?.Name ?? string.Empty
    };

    public void ResetForm()
    {
        // 1. Clear the text fields
        MeetingName = "";
        Description = "";
        MeetingTopics = "";

        SelectedTeam = null;
        SelectedVenue = null;
        Date = string.Empty;
        Time = "08:00-08:45";

        // 2. Clear the list of selected participants
        SelectedParticipants.Clear();
        // 3. Update the list of participants available for selection
        UpdateAvailableList();

        LogService.Instance.LogInfo("New Meeting Form has been reset.");
    }


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
            var (participantsRaw, topics) = AgendaParser.ParseMeetingAgenda(filePath);

            // 4. Show the parsed data in the UI
            MeetingTopics = topics;
            MatchPeopleInPlanWithTeam(participantsRaw);

            // Optional: If the description is empty, you can set a default description based on the agenda file name or other logic
            if (string.IsNullOrEmpty(Description))
                Description = $"Imported from agenda: {DateTime.Now:d}";
        }
        catch (Exception ex)
        {
            //  Parse error handling: Log the error and optionally show a message to the user
            LogService.Instance.LogError($"Failed to parse agenda file: {ex.Message}");
        }
    }

    private void MatchPeopleInPlanWithTeam(string participantsRaw)
    {
        if (!string.IsNullOrWhiteSpace(participantsRaw))
        {
            // Split the string by a semicolon or a comma.
            var parts = participantsRaw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string cleanPart = part.Trim();
                if (string.IsNullOrEmpty(cleanPart)) continue;

                // Extract the name and alias.
                string namePart = cleanPart;
                string aliasPart = "";

                // The regular expression searches for text inside parentheses.
                var match = Regex.Match(cleanPart, @"\(([^)]+)\)");
                if (match.Success)
                {
                    aliasPart = match.Groups[1].Value.Trim(); // "Alias"
                    namePart = cleanPart.Replace(match.Value, "").Trim(); // "Name
                }

                // Search for person in _allParticipants
                var foundParticipant = _allParticipants.FirstOrDefault(p =>
                    p.Name.Equals(namePart, StringComparison.OrdinalIgnoreCase) ||
                    p.Alias.Equals(aliasPart, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(aliasPart) && p.Name.Contains(aliasPart, StringComparison.OrdinalIgnoreCase)));

                if (foundParticipant != null)
                {
                    //  If found and it is not yet in the list of selected items, we add it.
                    if (!SelectedParticipants.Any(sp => sp.Id == foundParticipant.Id))
                    {
                        SelectedParticipants.Add(foundParticipant);
                    }
                }
            }
            //  We update the list of available options(to remove the ones that have been added).
            UpdateAvailableList();
        }
    }

    // -----------------------------------
    // Participants list operations
    // -----------------------------------
    public void RefreshParticipantsFromBase()
    {
        // Loading fresh data from the file
        var (people, _) = TeamStorageService.LoadData();
        _allParticipants = people;
        // Synchronization: if the selected participant was deleted from the database
        var toRemove = SelectedParticipants.Where(sp => !_allParticipants.Any(p => p.Id == sp.Id)).ToList();
        foreach (var r in toRemove) SelectedParticipants.Remove(r);
        // Updating the list of available options   
        UpdateAvailableList();
    }

    private void UpdateAvailableList()
    {
        AvailableToJoin.Clear();
        //  We take everyone from the database who isn't already on the selected list.
        var available = _allParticipants.Where(p =>
            !SelectedParticipants.Any(sp => sp.Id == p.Id));

        foreach (var p in available)
        {
            AvailableToJoin.Add(p);
        }
    }

    [RelayCommand]
    private void AddParticipant(Participant p)
    {
        if (p == null) return;
        SelectedParticipants.Add(p);
        UpdateAvailableList();
    }

    [RelayCommand]
    private void RemoveParticipant(Participant p)
    {
        if (p == null) return;
        SelectedParticipants.Remove(p);
        UpdateAvailableList();
    }
}