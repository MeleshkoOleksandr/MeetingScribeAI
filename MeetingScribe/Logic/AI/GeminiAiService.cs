using MeetingScribe.Logic.Meeting;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public class GeminiAiService : IAiService
{
    private readonly string _apiKey;
    private readonly string _url;
    private readonly bool _isPaid;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10) // for long resonses
    };

    public GeminiAiService(string apiKey, string modelName, string url, bool isPaid)
    {
        _apiKey = apiKey;
        _url = url;
        _isPaid = isPaid;
    }

    public async Task<AiResponseChunk?> ProcessChunkAsync(string rawText, string participants, string context, CancellationToken token)
    {
        var prompt = BuildCombinedPrompt(rawText, participants, context);

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
        };

        var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        string jsonPayload = JsonSerializer.Serialize(requestBody, serializerOptions);

        // Setting a timeout for the request depending on whether it's a paid or free model
        int maxRetries = _isPaid ? 2 : 5;
        int delayMs = _isPaid ? 1000 : 5000;

        for (int i = 0; i < maxRetries; i++)
        {
            // Creating new request  for each attempt to avoid issues with disposed content
            using var request = new HttpRequestMessage(HttpMethod.Post, _url);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request, token);

                // Server is bisy or rate-limited, retry after delay
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    System.Diagnostics.Debug.WriteLine($"Gemini 503 (Busy). Retry {i + 1}/{maxRetries}...");
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                    continue;
                }

                // Reading the response content
                var responseJson = await response.Content.ReadAsStringAsync();

                // Logging the response for debugging purposes
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Gemini API Error {response.StatusCode}: {responseJson}");
                    return null;
                }

                // Successful response, deserialize and return
                return ParseCombinedResponse(responseJson);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("User cancelled the AI process.");
                return null;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) throw; // If it's the last attempt, rethrow the exception
                await Task.Delay(delayMs);
            }

        }
        return null;
    }

    private string BuildCombinedPrompt(string raw, string participants, string context)
    {
        return $@"You are a professional meeting assistant.
        IMPORTANT: You are processing a SEGMENT of a larger meeting. 
        CONTEXT: {context}
        PARTICIPANTS: {participants}
        TASK:
             1. CLEANUP: Fix grammar, remove filler words ('umm', 'uh', 'like'). Fix technical terms.
             2. DIARIZATION: Identify speakers. Use names from the list if possible. Be consistent.
             3. CONSOLIDATE: This is crucial. The raw transcript is fragmented into very short pieces. 
                You MUST MERGE consecutive segments from the same speaker into single, long, coherent paragraphs. 
                Only create a new JSON object when the speaker changes or there is a significant pause/topic shift.
            4. TIMESTAMPS: Use the timestamp of the FIRST segment of the merged block as the 'Timestamp' for that block.
            5. SUMMARIZING: Summarize key points and decisions from THIS segment.

        RETURN FORMAT (Strict JSON):
        {{
          ""lines"": [
            {{ ""Timestamp"": ""[00:00:00]"", ""SpeakerName"": ""Name"", ""Text"": ""..."" }}
          ],
          ""segmentSummary"": ""Summary of what happened in this part...""
        }}

        RAW DATA:
        {raw}";
    }

    private AiResponseChunk? ParseCombinedResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            // 1. Пробиваемся к текстовому полю внутри ответа Google
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;

            var textResponse = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            if (string.IsNullOrEmpty(textResponse)) return null;

            // 2. Десериализуем текст в наш класс AiResponseChunk
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Важно: здесь мы используем textResponse (результат шага 1)
            return JsonSerializer.Deserialize<AiResponseChunk>(textResponse, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing Combined JSON: {ex.Message}");
            return null;
        }
    }

    public async Task<string> StitchSummariesAsync(List<string> partialSummaries, string meetingAgenda)
    {
        // 1. Собираем все кусочки в один текст
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);

        // 2. Формируем промпт для финальной сборки
        string prompt = $@"You are a professional meeting minutes assistant.
                        Based on the following partial summaries from different segments of the meeting, create a comprehensive and professional final protocol in Markdown format.
    
                        AGENDA CONTEXT: {meetingAgenda}
    
                        STRUCTURE:
                        # [Meeting Name] - Summary Protocol
                        ## Executive Summary (2-3 powerful sentences)
                        ## Key Discussion Points & Decisions
                        ## Action Items (Format as: [ ] Task | Assigned to | Deadline)
                        ## Next Steps

                        PARTIAL SUMMARIES TO SYNTHESIZE:
                        {combinedPartials}";

        // 3. Отправляем запрос
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.7 } // Чуть больше творчества для красивого текста
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _url);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return "Failed to generate summary.";

            // Извлекаем чистый текст из ответа (здесь парсинг проще, так как это не JSON-режим)
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stitching summaries: {ex.Message}");
            return "Error during summary generation.";
        }
    }
}