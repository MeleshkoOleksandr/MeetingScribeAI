using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.ViewModels;
using System.Diagnostics;

namespace MeetingScribe.UILogic;

[DebuggerDisplay("{Label}")]
public partial class NavigationItem : ObservableObject
{
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public PageNames Target { get; set; } 
    public string Description { get; set; } = "";
    public bool IsStartUp { get; set; } = false;
    public required ViewModelBase Page { get; set; }
}