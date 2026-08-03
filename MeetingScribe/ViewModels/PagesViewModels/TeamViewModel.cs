using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Wordprocessing;
using Humanizer;
using MeetingScribe.Enums;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MeetingScribe.ViewModels;

public partial class TeamViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<Participant> _participants = new();
    [ObservableProperty] private ObservableCollection<TeamGroup> _groups = new();
    [ObservableProperty] private Participant? _selectedParticipant;
    [ObservableProperty] private TeamGroup? _selectedGroup;
    [ObservableProperty] private string _participantSearchText = "";

    //List of groups that the selected participant is a member of
    public ObservableCollection<TeamGroup> SelectedParticipantGroups
    {
        get
        {
            if (SelectedParticipant == null) return new();

            var list = Groups.Where(g => SelectedParticipant.GroupIds.Contains(g.Id)).ToList();
            return new ObservableCollection<TeamGroup>(list);
        }
    }

    // List of available groups that the selected participant
    public ObservableCollection<TeamGroup> AvailableGroupsToJoin { get; } = new();

    private void UpdateAvailableGroups()
    {
        AvailableGroupsToJoin.Clear();
        if (SelectedParticipant is null) return;

        foreach (var g in Groups.Where(g => !SelectedParticipant.GroupIds.Contains(g.Id)))
            AvailableGroupsToJoin.Add(g);
    }

    public TeamViewModel()
    {
        var (people, groups) = TeamStorageService.LoadData();
        Participants = new ObservableCollection<Participant>(people);
        Groups = new ObservableCollection<TeamGroup>(groups);

        //If we adding or removing participants, we want to update the filtered list as well
        Participants.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredParticipants));
        // If we adding list (Add, Remove, Clear - save changes to disk
        Participants.CollectionChanged += (s, e) => AutoSaveToDisk();
        // Saving to disk and upadating the SelectedParticipantGroups when groups are changed
        Groups.CollectionChanged += (s, e) =>
        {
            RefreshMemberGroups();
            AutoSaveToDisk();
        };
        // Subcribe to changes in the Participants collection to auto-save to disk
        Participants.CollectionChanged += (s, e) => AutoSaveToDisk();
        //Show correct number of memmbers in group on UI  
        UpdateGroupMemberCounts();
        //Select first participant in the list if available as default
        SelectedParticipant = Participants.FirstOrDefault();
    }

    private void AutoSaveToDisk()
    {
        // Wrire to file when participants or groups are changed
        TeamStorageService.SaveData(Participants, Groups);
    }

    // ------   Filters  ------
    // ------------------------
    public IEnumerable<Participant> FilteredParticipants
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ParticipantSearchText))
                return Participants;

            string query = ParticipantSearchText.Trim();
            // 1. Find the IDs of all groups whose names match the search query.
            var matchingGroupIds = Groups
                .Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.Id)
                .ToList();
            // 2. Filter the participants based on three criteria:
            return Participants.Where(p =>
                // A. Name Match
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                // B. Alias ​​Match
                p.Alias.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                // C.The participant is a member of the group whose name we found in step 1.
                p.GroupIds.Any(id => matchingGroupIds.Contains(id))
            ).ToList();
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

    //Set avatar photo for selected user
    [RelayCommand]
    private async Task ChangePhoto()
    {
        if (SelectedParticipant == null) return;

        // Open file
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(ViewHelper.GetMainWindow());
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Profile Photo",
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count == 0) return;

        try
        {
            // Getting file path
            string sourcePath = files[0].Path.LocalPath;
            string photosDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Data", "Photos");
            Directory.CreateDirectory(photosDir);

            // New file name based on participant alias
            string newFileName = $"photo_{SelectedParticipant.Id}.jpg";
            string destinationPath = Path.Combine(photosDir, newFileName);

            // Using ImageHelper for crop and resize
            await Task.Run(() => ImageHelper.ResizeAndSavePhoto(sourcePath, destinationPath, 512));

            // Updating UI
            SelectedParticipant.PhotoFileName = null;
            SelectedParticipant.PhotoFileName = newFileName;

            AutoSaveToDisk();
        }
        catch (Exception ex)
        {
            LogService.Instance.LogError($"Error processing image for participant {SelectedParticipant.Name}: {ex.Message}");
            await LuminaMessageBox.Show("Image Error", "Could not process image: " + ex.Message, LuminaMessageBoxType.Danger);
        }
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


    // Add member to group
    [RelayCommand]
    private void AddGroupToMember(TeamGroup group)
    {
        if (SelectedParticipant == null || group == null) return;

        if (!SelectedParticipant.GroupIds.Contains(group.Id))
        {
            SelectedParticipant.GroupIds.Add(group.Id);
            RefreshMemberGroups();
            AutoSaveToDisk();
        }
    }

    // Remove member from group
    [RelayCommand]
    private void RemoveGroupFromMember(TeamGroup group)
    {
        if (SelectedParticipant == null || group == null) return;

        SelectedParticipant.GroupIds.Remove(group.Id);
        RefreshMemberGroups();
        AutoSaveToDisk();
    }

    private void RefreshMemberGroups()
    {
        UpdateAvailableGroups();
        UpdateGroupMemberCounts();
        OnPropertyChanged(nameof(SelectedParticipantGroups));
    }

    // OnSelectedParticipantChanged we call refresh for group lists
    partial void OnSelectedParticipantChanged(Participant? oldValue, Participant? newValue)
    {
        //  1.Unsubscribe from the old participant to prevent memory leaks.
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= Participant_PropertyChanged;
        }
        // 2. Subscribe the newly selected participant
        if (newValue != null)
        {
            newValue.PropertyChanged += Participant_PropertyChanged;
        }
        // Updating group lists
        RefreshMemberGroups();
    }

    private void Participant_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Ignore technical properties if necessary, and save.
        AutoSaveToDisk();
    }

    private void UpdateGroupMemberCounts()
    {
        foreach (var group in Groups)
        {
            // We count the participants who have this group's ID in their GroupIds list.
            group.MemberCount = Participants.Count(p => p.GroupIds.Contains(group.Id));
        }
    }
}