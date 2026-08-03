using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Enums;
using MeetingScribe.Logic.Services;
using MeetingScribe.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingScribe.ViewModels;

public partial class LogsViewModel : ViewModelBase
{
    // Search query for filtering logs
    [ObservableProperty] private string _searchQuery = "";

    // Link to the main log entries collection
    public ObservableCollection<LogEntry> AllEntries => LogService.Instance.Entries;

    // Filtered log entries based on the search query
    public IEnumerable<LogEntry> FilteredEntries
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
                return AllEntries;

            return AllEntries.Where(e =>
                e.Message.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Level.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.TimeLabel.Contains(SearchQuery));
        }
    }

    public LogsViewModel()
    {
        // Subscribe to changes in the main collection to update the filtered list
        AllEntries.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredEntries));
    }

    // Notify the UI when the search string changes
    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredEntries));
    }

    [RelayCommand]
    private async Task ClearLogs()
    {
        var result = await LuminaMessageBox.Show(
            "Clear Log?",
            "Are you sure you want to clear the current session logs? History files on disk will remain.",
            LuminaMessageBoxType.Danger, "Clear");

        if (result == LuminaMessageBox.MessageBoxResult.Confirm)
        {
            LogService.Instance.Entries.Clear();
            OnPropertyChanged(nameof(FilteredEntries));
        }
    }

    [RelayCommand]
    private async Task ExportLog()
    {
        try
        {
            // Generating a filename for export
            string fileName = $"Manual_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", fileName);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(AllEntries, options);

            await File.WriteAllTextAsync(path, json);

            await LuminaMessageBox.Show("Export Successful", $"Log has been saved to:\n{path}", LuminaMessageBoxType.Message);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("Failed to export logs", LogLevel.Warning, ex.Message);
        }
    }
}