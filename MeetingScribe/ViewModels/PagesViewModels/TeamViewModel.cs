using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic.Enums;
using MeetingScribe.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MeetingScribe.ViewModels;

public partial class TeamViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<Participant> _participants = new();
    [ObservableProperty] private ObservableCollection<TeamGroup> _groups = new();
    [ObservableProperty] private Participant? _selectedParticipant;
    [ObservableProperty] private TeamGroup? _selectedGroup;
    [ObservableProperty] private string _participantSearchText = "";

    public TeamViewModel()
    {
        var (people, groups) = TeamStorageService.LoadData();
        Participants = new ObservableCollection<Participant>(people);
        Groups = new ObservableCollection<TeamGroup>(groups);

        //If we adding or removing participants, we want to update the filtered list as well
        Participants.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredParticipants));
        // If we adding list (Add, Remove, Clear - save changes to disk
        Participants.CollectionChanged += (s, e) => AutoSaveToDisk();
        Groups.CollectionChanged += (s, e) => AutoSaveToDisk();
    }

    private void AutoSaveToDisk()
    {
        // Wrire to file when participants or groups are changed
        TeamStorageService.SaveData(Participants, Groups);
        System.Diagnostics.Debug.WriteLine("Structure auto-saved to disk.");
    }

    // ------   Filters  ------
    // ------------------------
    public IEnumerable<Participant> FilteredParticipants
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ParticipantSearchText))
                return Participants;

            return Participants.Where(p =>
                p.Name.Contains(ParticipantSearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Alias.Contains(ParticipantSearchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnParticipantSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredParticipants));
    }

    // ------   Participant  ------
    // ----------------------------
    [RelayCommand]
    private void AddParticipant()
    {
        var p = new Participant { Name = "New Member", Alias = "NM" };
        Participants.Add(p);
        SelectedParticipant = p;
    }

    [RelayCommand]
    private async Task DeleteParticipant()
    {
        if (SelectedParticipant == null) return;

        var res = await LuminaMessageBox.Show(
            "Delete Member?",
            $"Are you sure you want to remove {SelectedParticipant.Name}?",
            LuminaMessageBoxType.Danger, "Remove");

        if (res == LuminaMessageBox.MessageBoxResult.Confirm)
        {
            Participants.Remove(SelectedParticipant);
            SelectedParticipant = null;
        }
    }

    [RelayCommand]
    private async Task ClearParticipants()
    {
        if (Participants.Count == 0) return;

        var res = await LuminaMessageBox.Show(
            "Clear All?",
            "This will remove EVERY participant from the database. This cannot be undone.",
            LuminaMessageBoxType.Danger, "Clear All");

        if (res == LuminaMessageBox.MessageBoxResult.Confirm)
        {
            Participants.Clear();
            SelectedParticipant = null;
        }
    }

    [RelayCommand]
    private async Task SaveAll()
    {
        // Saving current state to disk
        AutoSaveToDisk();

        await LuminaMessageBox.Show(
            "Saved",
            "All member details have been updated successfully.",
            LuminaMessageBoxType.Message);
    }

    // ------   Groups  ------
    // -----------------------
    [RelayCommand]
    private async Task AddGroup()
    {
        var dialog = new AddGroupWindow { DataContext = new AddGroupViewModel() };
        var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var result = await dialog.ShowDialog<TeamGroup?>(owner!);

        if (result != null)
        {
            Groups.Add(result); // AutoSaveToDisk will be called due to CollectionChanged event
        }
    }

    [RelayCommand]
    private async Task DeleteGroup()
    {
        if (SelectedGroup == null) return;
        var res = await LuminaMessageBox.Show("Delete Group?", $"Delete {SelectedGroup.Name}?", LuminaMessageBoxType.Danger);
        if (res == LuminaMessageBox.MessageBoxResult.Confirm) Groups.Remove(SelectedGroup);
    }

    [RelayCommand]
    private async Task ClearGroups()
    {
        var res = await LuminaMessageBox.Show("Clear Groups?", "Delete all groups?", LuminaMessageBoxType.Danger);
        if (res == LuminaMessageBox.MessageBoxResult.Confirm) Groups.Clear();
    }

}