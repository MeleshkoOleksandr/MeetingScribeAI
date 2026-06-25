using MeetingScribe.Logic.AI;
using MeetingScribe.Logic.Meeting;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public class GeminiAiService : IAiService
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _url;
    private static readonly HttpClient _httpClient = new();

    public GeminiAiService(string apiKey, string model, string url)
    {
        _apiKey = apiKey;
        _model = model;
        _url = url;
    }

    public async Task<List<TranscriptLine>> RefineAndDiarizeAsync(string rawTranscript, string participants, string meetingContext)
    {
        var prompt = BuildPrompt(rawTranscript, participants, meetingContext);

        // Формируем запрос согласно API Google Gemini
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.1 } // Низкая температура для точности JSON
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var fullUrl = $"{_url}?key={_apiKey}";

        var response = await _httpClient.PostAsync(fullUrl, new StringContent(jsonRequest, Encoding.UTF8, "application/json"));
        var responseJson = await response.Content.ReadAsStringAsync();

        return ParseGeminiResponse(responseJson);
    }

    private string BuildPrompt(string raw, string participants, string context)
    {
        return $@"You are a professional meeting assistant.
        CONTEXT: {context}
        PARTICIPANTS: {participants}
        
        TASK:
        1. Clean up the transcript (fix grammar, remove fillers like 'umm', 'uh').
        2. Identify who is speaking based on context and participants list.
        3. Format the output as a JSON array of objects.
        
        REQUIRED JSON FORMAT:
        [
          {{ ""Timestamp"": ""[00:00:10]"", ""SpeakerName"": ""John Doe"", ""Text"": ""Hello everyone."" }}
        ]

        RAW TRANSCRIPT TO PROCESS:
        {raw}";
    }

    private List<TranscriptLine> ParseGeminiResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            // Извлекаем текст из структуры ответа Gemini
            var contentText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            if (string.IsNullOrEmpty(contentText)) return new List<TranscriptLine>();

            // Очищаем текст от Markdown-обертки ```json ... ```
            var jsonMatch = Regex.Match(contentText, @"\[\s*\{.*\}\s*\]", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                return JsonSerializer.Deserialize<List<TranscriptLine>>(jsonMatch.Value) ?? new();
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"JSON Parse Error: {ex.Message}"); }
        return new List<TranscriptLine>();
    }
}