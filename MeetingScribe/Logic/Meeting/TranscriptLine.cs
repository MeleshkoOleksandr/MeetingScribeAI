using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingScribe.Logic.Meeting;

public partial class TranscriptLine : ObservableObject
{
    public string Timestamp { get; set; } = "00:00:00";
    public string Text { get; set; } = "";
    [ObservableProperty] private string _speakerName = "Unknown";
    public bool IsActionItem { get; set; }
}