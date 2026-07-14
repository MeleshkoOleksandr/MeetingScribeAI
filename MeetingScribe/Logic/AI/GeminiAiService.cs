using MeetingScribe.Logic.Meeting;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public class GeminiAiService : IAiService
{
    private readonly string _apiKey;
    private readonly string _url;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10) // for long resonses
    };

    public GeminiAiService(string apiKey, string modelName, string url)
    {
        _apiKey = apiKey;
        _url = url;
    }

    public async Task<List<TranscriptLine>> RefineAndDiarizeAsync(string rawTranscript, string participants, string meetingContext)
    {
        var finalResult = new List<TranscriptLine>();
        var allLines = rawTranscript.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // 1. Группируем строки по 15-минутным интервалам
        var chunks = GroupLinesByTime(allLines, intervalMinutes: 15);

        int currentChunkIndex = 1;
        foreach (var chunk in chunks)
        {
            System.Diagnostics.Debug.WriteLine($"Processing AI Chunk {currentChunkIndex} of {chunks.Count}...");

            // 2. Формируем специальный промпт для куска, чтобы ИИ понимал контекст
            string chunkText = string.Join("\n", chunk);
            string chunkContext = $"{meetingContext} (Part {currentChunkIndex} of {chunks.Count})";

            // 3. Отправляем запрос (с нашей логикой Retry, которую мы писали ранее)
            var refinedChunk = await SendChunkToGeminiAsync(chunkText, participants, chunkContext);

            if (refinedChunk != null)
            {
                finalResult.AddRange(refinedChunk);
            }

            currentChunkIndex++;

            // Небольшая пауза между запросами, чтобы Google не заблокировал за спам
            await Task.Delay(1000);
        }

        return finalResult;
    }

    // Метод для разбивки текста по времени
    private List<List<string>> GroupLinesByTime(string[] lines, int intervalMinutes)
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
    private int ParseTimestampToSeconds(string line)
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

    // Вынесенная логика одного запроса к ИИ (Body + Headers + Send)
    private async Task<List<TranscriptLine>> SendChunkToGeminiAsync(string text, string participants, string context)
    {
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = BuildPrompt(text, participants, context) } } } },
            generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var request = new HttpRequestMessage(HttpMethod.Post, _url);
        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return null;

        return ParseGeminiResponse(responseJson);
    }

    private string BuildPrompt(string raw, string participants, string context)
    {
        // Параметр context теперь будет содержать строку типа "Project Update (Part 2 of 4)"
        return $@"You are a professional meeting assistant. 
    
        IMPORTANT: You are processing a SEGMENT of a larger meeting. 
        CONTEXT: {context}
        PARTICIPANTS: {participants}
    
        TASK:
        1. Refine the transcript: fix grammar, industry terms, and remove filler words ('umm', 'like', etc.).
        2. Identify speakers based on context. 
            - Use the PROVIDED PARTICIPANT LIST if names are mentioned or can be inferred.
            - BE CONSISTENT: If a speaker is identified as 'Speaker 1', ensure they remain 'Speaker 1' throughout this segment.
            - If you can identify a name (e.g. someone says 'Hi, John'), use that name instead of a generic ID.
        3. Break the text into logical, readable dialogue segments.
        4. Preserve original timestamps exactly as they appear in the source.

        RETURN FORMAT (Strict JSON Array):
        [
            {{ ""Timestamp"": ""[00:00:00]"", ""SpeakerName"": ""Name"", ""Text"": ""Recognized speech"" }}
        ]

        RAW TRANSCRIPT SEGMENT:
        {raw}";
    }

    private List<TranscriptLine> ParseGeminiResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);

            // Проходим по иерархии ответа Google
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return new List<TranscriptLine>();

            var textResponse = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            if (string.IsNullOrEmpty(textResponse)) return new List<TranscriptLine>();

            // Десериализуем чистый JSON массив
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<TranscriptLine>>(textResponse, options) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing JSON from Gemini: {ex.Message}");
            return new List<TranscriptLine>();
        }
    }
}