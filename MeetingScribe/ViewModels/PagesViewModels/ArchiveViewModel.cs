using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
    // Action to be invoked when a meeting is opened
    private Action<MeetingSession> _onOpenRequest;

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
    public void InitArchiveViewModel(Action<MeetingSession> onOpenRequest)
    {
        _onOpenRequest = onOpenRequest;
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
                    if (session != null)
                    {
                        session.FolderPath = dir; // Store the folder path in the session object
                        Meetings.Add(session);
                    }
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
        if (SelectedMeeting != null)
        {
            _onOpenRequest?.Invoke(SelectedMeeting);
        }
    }

    [RelayCommand]
    private async Task DeleteMeeting()
    {
        // Is meeting selected? If not, exit the method
        if (SelectedMeeting == null) return;

        var result =  await LuminaMessageBox.Show("Delete Recording?", $"Are you sure you want to permanently delete '{SelectedMeeting.Name}'? This action cannot be undone.",
            LuminaMessageBoxType.Danger, "Delete Forever");

        if (result == LuminaMessageBox.MessageBoxResult.Confirm)
        {
            try
            {
                // Chreck if the folder exists before attempting to delete it
                string pathToDelete = SelectedMeeting.FolderPath;

                if (Directory.Exists(pathToDelete))
                {
                    // Delete the directory and all its contents
                    Directory.Delete(pathToDelete, true);

                    // Delete the meeting from the observable collection
                    Meetings.Remove(SelectedMeeting);

                    // Select the first meeting in the filtered list
                    SelectedMeeting = null;
                    RefreshSelection();

                    System.Diagnostics.Debug.WriteLine($"Meeting deleted: {pathToDelete}");
                }
            }
            catch (Exception ex)
            {
                // Some error occurred while deleting the meeting, log the error message
                System.Diagnostics.Debug.WriteLine($"Error deleting meeting: {ex.Message}");
            }
        }
    }
}