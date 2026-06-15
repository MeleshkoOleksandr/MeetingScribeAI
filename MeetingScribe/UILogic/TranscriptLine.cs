namespace MeetingScribe.UILogic;

public class TranscriptLine
{
    public string Timestamp { get; set; } = "00:00:00";
    public string Text { get; set; } = "";
    public bool IsAiAnalyzing { get; set; } = false; // For the green highlight
}