using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingScribe.Logic.Meeting;

public static class TextOparations
{

    // Метод для разбивки текста по времени
    public static List<List<string>> GroupLinesByTime(string[] lines, int intervalMinutes)
    {
        var chunks = new List<List<string>>();
        var currentChunk = new List<string>();
        int currentIntervalLimit = intervalMinutes * 60; // Переводим в секунды

        foreach (var line in lines)
        {
            int lineSeconds = ParseTimestampToSeconds(line);

            // Если время строки превышает текущий лимит чанка — создаем новый чанк
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

    // Помощник для извлечения секунд из строки типа [00:15:30]
    private static int ParseTimestampToSeconds(string line)
    {
        try
        {
            // Ищем [00:00:00] в начале строки
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
