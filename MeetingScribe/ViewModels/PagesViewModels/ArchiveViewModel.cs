using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace MeetingScribe.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    private readonly TranscriptionService _transcriptionService = new TranscriptionService();
    private readonly string _whisperPath = "";

    [ObservableProperty] private ObservableCollection<MeetingSession> _meetings = new();
    [ObservableProperty] private MeetingSession? _selectedMeeting;
    [ObservableProperty] private string _searchText = "";

    // Список для отображения с учетом фильтрации
    public IEnumerable<MeetingSession> FilteredMeetings =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Meetings
            : Meetings.Where(m => m.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public ArchiveViewModel()
    {
    }

    public ArchiveViewModel(TranscriptionService transcriptionService, string whisperPath)
    {
        _transcriptionService = transcriptionService;
        _whisperPath = whisperPath;
        LoadArchive();
    }

    public void LoadArchive()
    {
        Meetings.Clear();
        string archivePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Meeting Archive");

        if (!Directory.Exists(archivePath)) return;

        foreach (var dir in Directory.GetDirectories(archivePath))
        {
            string jsonPath = Path.Combine(dir, "session_data.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var json = File.ReadAllText(jsonPath);
                    var session = JsonSerializer.Deserialize<MeetingSession>(json);
                    if (session != null) Meetings.Add(session);
                }
                catch (Exception ex)
                {             
                    System.Diagnostics.Debug.WriteLine($"Error on reading archive meeting from file: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    private void OpenSelectedMeeting()
    {
        if (SelectedMeeting == null) return;

        // GO to the meeting page with the selected meeting session
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredMeetings));
}