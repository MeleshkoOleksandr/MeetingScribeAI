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
    // Sort options for the meetings list
    public List<string> SortOptions { get; } = ["▽ DATE", "△ DATE", "▽ NAME", "△ NAME"];
    [ObservableProperty] private string _selectedSortOption = "▽ DATE";

    // Observable collection of meetings in the archive
    [ObservableProperty] private ObservableCollection<MeetingSession> _meetings = new();
    [ObservableProperty] private MeetingSession? _selectedMeeting;
    // Search text for filtering the meetings list
    [ObservableProperty] private string _searchText = "";

    // Refresh the filtered meetings list when the search text or sort option changes
    partial void OnSearchTextChanged(string value) => RefreshSelection();
    partial void OnSelectedSortOptionChanged(string value) => RefreshSelection();

    // List of meetings filtered by the search text
    public IEnumerable<MeetingSession> FilteredMeetings
    {
        get
        {
            var result = string.IsNullOrWhiteSpace(SearchText)
                ? Meetings
                : Meetings.Where(m => m.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            // apply sorting based on the selected sort option
            result = SelectedSortOption switch
            {
                "▽ DATE" => result.OrderByDescending(m => m.StartTime),
                "△ DATE" => result.OrderBy(m => m.StartTime),
                "▽ NAME" => result.OrderBy(m => m.Name),
                "△ NAME" => result.OrderByDescending(m => m.Name),
                _ => result
            };

            return result.ToList();
        }
    }

    // Refresh the filtered meetings list and reset the selected meeting
    private void RefreshSelection()
    {
        OnPropertyChanged(nameof(FilteredMeetings));
        // Reset the selected meeting to the first one in the filtered list
        SelectedMeeting = FilteredMeetings.FirstOrDefault();
    }

    // Command to apply the selected sort option
    [RelayCommand]
    public void ApplySort(string option)
    {
        SelectedSortOption = option;
    }

    public ArchiveViewModel()
    {
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
        RefreshSelection();
    }

    [RelayCommand]
    private void OpenSelectedMeeting()
    {
        if (SelectedMeeting == null) return;

        // GO to the meeting page with the selected meeting session
    }

}