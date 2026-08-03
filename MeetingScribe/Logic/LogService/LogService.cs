using MeetingScribe.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MeetingScribe.Logic.Services;

public class LogService 
{
    // Singleton for access from anywhere: LogService.Instance.Log(...)
    public static LogService Instance { get; } = new LogService();

    public ObservableCollection<LogEntry> Entries { get; } = new();
    public event Action? OnCriticalError; // Event to activate the bell

    private readonly string _mdFilePath;
    private readonly string _jsonFilePath;

    private LogService()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(folder);

        string date = DateTime.Now.ToString("yyyy-MM-dd");
        _mdFilePath = Path.Combine(folder, $"log_{date}.md");
        _jsonFilePath = Path.Combine(folder, $"log_{date}.json");
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

        // Add a list to the UI (asynchronously)
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            Entries.Insert(0, entry);
            if (level != LogLevel.Info) OnCriticalError?.Invoke();
        });

        // Save to files IMMEDIATELY
        WriteToFiles(entry);
    }


    private readonly object _fileLock = new object(); // To prevent simultaneous writing from different streams

    private void WriteToFiles(LogEntry entry)
    {
        lock (_fileLock)
        {
            try
            {
                // Write in Markdown
                string logLine = $"| {entry.TimeLabel} | {entry.Level.ToString().ToUpper()} | {entry.Message} |\n";
                File.AppendAllText(_mdFilePath, logLine);

                // Write in JSON
                string jsonLine = JsonSerializer.Serialize(entry) + Environment.NewLine;
                File.AppendAllText(_jsonFilePath, jsonLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Could not write log to disk: {ex.Message}");
            }
        }
    }

    public List<LogEntry> LoadAllLogsFromDisk()
    {
        var allLogs = new List<LogEntry>();
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        if (!Directory.Exists(folder)) return allLogs;

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                // Reading a file line by line
                var lines = File.ReadAllLines(file);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var log = JsonSerializer.Deserialize<LogEntry>(line);
                    if (log != null) allLogs.Add(log);
                }
            }
            catch 
            { 
                LogError($"Failed to read log file: {file}");
            }
        }
        return allLogs;
    }
}