using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;
using Humanizer;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic.Enums;
using MeetingScribe.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
            // 2. Пути
            string sourcePath = files[0].Path.LocalPath;
            string photosDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Data", "Photos");
            Directory.CreateDirectory(photosDir);

            // Имя файла: photo_ID.jpg (берем расширение оригинала)
            string extension = Path.GetExtension(sourcePath);
            string newFileName = $"photo_{SelectedParticipant.Id}{extension}";
            string destinationPath = Path.Combine(photosDir, newFileName);

            // 3. Копируем файл (перезаписываем если был)
            File.Copy(sourcePath, destinationPath, true);

            // 4. Обновляем модель
            SelectedParticipant.PhotoFileName = newFileName;

            // Уведомляем UI (чтобы конвертер перечитал файл)
            OnPropertyChanged(nameof(SelectedParticipant));
            AutoSaveToDisk();
        }
        catch (Exception ex) { /* MessageBox с ошибкой */ }
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

}