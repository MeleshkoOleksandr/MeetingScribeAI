namespace MeetingScribe.Logic.Meeting;

public class TranscriptLine
{
    public string Timestamp { get; set; } = "00:00:00";
    public string Text { get; set; } = "";
    public string SpeakerName { get; set; } = "Unknown";
    public bool IsActionItem { get; set; }

}