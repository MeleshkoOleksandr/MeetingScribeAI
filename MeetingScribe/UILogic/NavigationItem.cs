using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingScribe.UILogic;

public partial class NavigationItem : ObservableObject
{
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public PageNames Target { get; set; } 
    public string Description { get; set; } = "";
    public bool IsStartUp { get; set; } = false;
}