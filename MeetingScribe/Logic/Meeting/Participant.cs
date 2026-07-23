using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace MeetingScribe.Logic.Meeting;

public partial class Participant : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _alias = ""; // Short alias (JD, AM)
    [ObservableProperty] private string _position = "";
    [ObservableProperty] private string _tagColor = "#b7e97e"; // HEX color
    [ObservableProperty] private string? _photoFileName;
    public List<Guid> GroupIds { get; set; } = new(); // Links to groups this participant belongs to
}