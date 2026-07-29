using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MeetingScribe.UILogic.Enums;

namespace MeetingScribe.Logic.Services;

public class LogService
{
    // Singleton for access from anywhere: LogService.Instance.Log(...)
    public static LogService Instance { get; } = new LogService();

    public ObservableCollection<LogEntry> Entries { get; } = new();
    public event Action? OnCriticalError; // Event to activate the bell

    private readonly string _logFilePath;

    private LogService()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(folder);
        _logFilePath = Path.Combine(folder, $"log_{DateTime.Now:yyyy-MM-dd}.md");
    }

    public void LogException(Exception ex, string contextMessage)
    {
        Log($"{contextMessage}: {ex.Message}", LogLevel.Critical, ex.StackTrace);
    }

    public void LogInfo(string contextMessage)
    {
        Log(contextMessage, LogLevel.Info);
    }

    public void LogError(string contextMessage)
    {
        Log(contextMessage, LogLevel.Warning);
    }

    public void Log(string message, LogLevel level = LogLevel.Info, string? stackTrace = null)
    {
        var entry = new LogEntry { Message = message, Level = level, StackTrace = stackTrace };

        // Add to collection (for UI)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Entries.Insert(0, entry); //Newest at the top
            if (level != LogLevel.Info) OnCriticalError?.Invoke();
        });

        // Save to a file (Markdown format)
        string logLine = $"| {entry.TimeLabel} | {level.ToString().ToUpper()} | {message} |\n";
        if (!string.IsNullOrEmpty(stackTrace)) logLine += $"```\n{stackTrace}\n```\n";

        File.AppendAllText(_logFilePath, logLine);
    }
}