using System;
using MeetingScribe.Enums;

namespace MeetingScribe.Logic.Services;

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public string? StackTrace { get; set; }
    public string TimeLabel => Timestamp.ToString("HH:mm:ss");
    public string DateLabel => Timestamp.ToString("MMMM dd, yyyy");
}