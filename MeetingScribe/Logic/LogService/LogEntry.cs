using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.Enums;
using System;

namespace MeetingScribe.Logic.Services;

public partial class LogEntry : ObservableObject
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TimeLabel => Timestamp.ToString("HH:mm:ss");
    public string DateLabel => Timestamp.ToString("MMMM dd, yyyy");


    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";

    public string? StackTrace { get; set; }
    public bool HasStackTrace => !string.IsNullOrWhiteSpace(StackTrace);


    [ObservableProperty] private bool _isExpanded = false;
}