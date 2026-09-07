using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingScribe.Logic.Meeting;

public static class TextOparations
{

    // Method to group lines by time intervals
    public static List<List<string>> GroupLinesByTime(string[] lines, int intervalMinutes)
    {
        var chunks = new List<List<string>>();
        var currentChunk = new List<string>();
        int currentIntervalLimit = intervalMinutes * 60; // Convert to seconds

        foreach (var line in lines)
        {
            int lineSeconds = ParseTimestampToSeconds(line);

            // If the line's timestamp exceeds the current interval limit — create a new chunk
            if (lineSeconds >= currentIntervalLimit)
            {
                if (currentChunk.Count > 0) chunks.Add(new List<string>(currentChunk));
                currentChunk.Clear();
                currentIntervalLimit += intervalMinutes * 60;
            }
            currentChunk.Add(line);
        }

        if (currentChunk.Count > 0) chunks.Add(currentChunk);
        return chunks;
    }

    // Helper method to extract seconds from a string of the format [00:15:30]
    private static int ParseTimestampToSeconds(string line)
    {
        try
        {
            // Find the [00:00:00] pattern at the beginning of the string
            int start = line.IndexOf('[');
            int end = line.IndexOf(']');
            if (start != -1 && end > start)
            {
                string ts = line.Substring(start + 1, end - start - 1);
                if (TimeSpan.TryParse(ts, out var time))
                {
                    return (int)time.TotalSeconds;
                }
            }
        }
        catch { }
        return 0;
    }
}
