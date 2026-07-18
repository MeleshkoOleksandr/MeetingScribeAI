using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;

namespace MeetingScribe.ViewModels;

public partial class TeamViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<Participant> _participants = new();
    [ObservableProperty] private ObservableCollection<TeamGroup> _groups = new();
    [ObservableProperty] private Participant? _selectedParticipant;
    [ObservableProperty] private TeamGroup? _selectedGroup;

    public TeamViewModel()
    {
        var (people, groups) = TeamStorageService.LoadData();
        Participants = new ObservableCollection<Participant>(people);
        Groups = new ObservableCollection<TeamGroup>(groups);
    }

    [RelayCommand]
    private void AddParticipant()
    {
        var p = new Participant { Name = "New Member", Alias = "NM" };
        Participants.Add(p);
        SelectedParticipant = p;
    }

    [RelayCommand]
    private void SaveAll() => TeamStorageService.SaveData(Participants, Groups);

    // The logic behind automatic saving when any field is changed
    partial void OnSelectedParticipantChanged(Participant? value) => SaveAll();
}