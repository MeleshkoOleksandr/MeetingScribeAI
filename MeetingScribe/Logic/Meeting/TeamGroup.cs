using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace MeetingScribe.Logic.Meeting;

public partial class TeamGroup : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _icon = "AccountGroup"; // MaterialIconKind
}