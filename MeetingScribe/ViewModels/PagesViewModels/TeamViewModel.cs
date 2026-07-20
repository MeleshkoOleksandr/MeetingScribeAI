using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
    }

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

    [RelayCommand]
    private void AddParticipant()
    {
        var p = new Participant { Name = "New Member", Alias = "NM" };
        Participants.Add(p);
        SelectedParticipant = p;
    }

    [RelayCommand]
    private void SaveAll() => TeamStorageService.SaveData(Participants, Groups); // TODO Change Participants and Groups separatly

    // The logic behind automatic saving when any field is changed
    partial void OnSelectedParticipantChanged(Participant? value) => SaveAll();
}