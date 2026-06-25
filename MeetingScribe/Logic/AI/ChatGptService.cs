
using MeetingScribe.Logic.AI;
using MeetingScribe.Logic.Meeting;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public class ChatGptService : IAiService
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _url;
    private static readonly HttpClient _httpClient = new();

    public ChatGptService(string apiKey, string model, string url)
    {
        _apiKey = apiKey;
        _model = model;
        _url = url;
    }

    public async Task<List<TranscriptLine>> RefineAndDiarizeAsync(string rawTranscript, string participants, string meetingContext)
    {
        var prompt = "Follow the same logic as described in the system message..."; // (Аналогичный промпт)

        var requestBody = new
        {
            model = _model,
            messages = new[] {
                new { role = "system", content = "You format meeting transcripts into JSON." },
                new { role = "user", content = prompt }
            },
            response_format = new { type = "json_object" } // ChatGPT умеет возвращать чистый JSON
        };

        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.PostAsync(_url, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
        var resultJson = await response.Content.ReadAsStringAsync();

        // Парсинг ответа OpenAI (другая структура JSON)
        return ParseOpenAiResponse(resultJson);
    }

    private List<TranscriptLine> ParseOpenAiResponse(string json)
    {
        // Логика извлечения из choices[0].message.content
        return new List<TranscriptLine>();
    }
}