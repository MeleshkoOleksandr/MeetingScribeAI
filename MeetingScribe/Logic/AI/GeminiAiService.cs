using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Spreadsheet;
using MeetingScribe.Enums;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
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
        var prompt = PromtHelper.BuildCombinedPrompt(rawText, participants, context);

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
                    LogService.Instance.Log($"Gemini 503 (Busy). Retry {i + 1}/{maxRetries}...", LogLevel.Warning);             
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                    continue;
                }

                // Reading the response content
                var responseJson = await response.Content.ReadAsStringAsync();

                // Logging the response for debugging purposes
                if (!response.IsSuccessStatusCode)
                {
                    LogService.Instance.Log($"Gemini API Error {response.StatusCode}: {responseJson}", LogLevel.Critical); 
                    return null;
                }

                // Successful response, deserialize and return
                return ParseCombinedResponse(responseJson);
            }
            catch (OperationCanceledException)
            {
                LogService.Instance.Log("AI process was cancelled by the user.", LogLevel.Info);
                return null;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) LogService.Instance.LogException(ex,"Last Api call returned with Error"); // If it's the last attempt
                await Task.Delay(delayMs);
            }

        }
        return null;
    }

    private AiResponseChunk? ParseCombinedResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            // We're trying to access the text field inside the Google response
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;

            var textResponse = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            if (string.IsNullOrEmpty(textResponse)) return null;

            // Deserialize the text into our AiResponseChunk class 
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<AiResponseChunk>(textResponse, options);
        }
        catch (Exception ex)
        {
            LogService.Instance.LogException(ex, "Error parsing Combined JSON response");
            return null;
        }
    }

    public  async Task<string> StitchSummariesAsync(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        return await SendSammaryPromt(PromtHelper.GeneralSummariesPromt(partialSummaries, meetingAgenda, langCode));
    }

    public  async Task<string> TemplateSummariesAsync(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        return await SendSammaryPromt(PromtHelper.TemplateSummariesPromt(partialSummaries, meetingAgenda, langCode));
    }

    private async Task<string> SendSammaryPromt(string prompt)
    {
        // Sending a request
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.7 }
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

            //Extracting the text from the response (here parsing is simpler since it's not JSON mode)
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "";
        }
        catch (Exception ex)
        {
            LogService.Instance.LogException(ex, "Error stitching summaries");
            return "Error during summary generation.";
        }
    }
}