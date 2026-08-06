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

    private string BuildCombinedPrompt(string raw, string participants, string context)
    {
        return $@"You are a professional meeting assistant specializing in verbatim transcript post-processing.
                IMPORTANT: You are processing a SEGMENT of a larger meeting. 
                CONTEXT: {context}
                LIST OF EXPECTED PARTICIPANTS: {participants}

                TASK:
                1. CLEANUP (STRICT VERBATIM): Remove filler words ('umm', 'uh', 'like', 'sì', 'ecco', 'diciamo così'), stuttering, and accidental word repetitions (e.g., if a speaker says 'che è un po\' di potenza, che è un po\' di potenza', keep it only once). Fix obvious speech-to-text typos in technical terms. 
                   CRITICAL: DO NOT paraphrase, DO NOT summarize the text inside the 'Text' field, DO NOT improve grammar if it changes the speaker's original phrasing, and DO NOT use sophisticated vocabulary. Keep the original words, word order, and spoken style exactly as they are.

                2. DIARIZATION: Identify speakers. Based on the conversation context and the LIST OF EXPECTED PARTICIPANTS, identify who is speaking.
                   - If you are absolutely sure of the name, use the full name from the list.
                   - If you are unsure, use 'Speaker 1', 'Speaker 2', etc.
                   - DO NOT invent names that are not in the list or the transcript.

                3. CONSOLIDATE: The raw transcript is fragmented into very short time-coded lines. You MUST MERGE consecutive lines from the same speaker into single, long paragraphs. 
                   CRITICAL: 'Merging' means simply gluing the original text pieces together into a continuous text block after doing the CLEANUP. Do not rewrite the sentence structures during consolidation.
                   Only create a new JSON object when the speaker changes or there is a significant pause/topic shift.

                4. TIMESTAMPS: Use the timestamp of the FIRST segment of the merged block as the 'Timestamp' for that block.

                5. SUMMARIZING (FACT-PRESERVING DIGEST): Create a comprehensive, dense but complete summary of THIS segment specifically optimized for later consolidation into a final meeting summary.
                   CRITICAL: Do not generalize. You must preserve:
                   - Key discusion topics and points
                   - Specific numbers, metrics, dates, and deadlines (e.g., ""SIR meeting on August 4th and 6th"").
                   - Names of projects, documents, and tools 
                   - Names of people mentioned and their roles or actions 
                   - Action items, decisions, and specific problems raised.
                
                6. Do not translate the meeting transcription. The result and summary must be in the same language as the input data. 

                STRICT CONSTRAINT:
                The 'Text' field must contain the EXACT spoken words of the speaker (minus fillers/duplications). It must remain in the original language (Italian in this case). Never turn spoken, slightly chaotic speech into formal written business prose.

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

    public async Task<string> StitchSummariesAsync(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        // Stitching summaries together with the meeting agenda for context
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);

        string langInstruction = GetLanguageInstruction(langCode);

        // Making a prompt for the AI to make summaries
        string prompt = $@"You are a professional meeting minutes assistant.

                        {langInstruction}

                        TASK: Based on the following partial summaries from different segments of the meeting, create a comprehensive and professional final protocol in Markdown format.
                                     
                        AGENDA CONTEXT: {meetingAgenda}
    
                        (Important: All headings in the final result must be in the same language as the main text)
                        STRUCTURE: 
                        # [Meeting Name]
                        ## Executive Summary (3-6 powerful sentences)
                        ## Key Discussion Points & Decisions
                        ## Action Items (Format as: Task | Assigned to | Deadline)
                        ## Next Steps

                        PARTIAL SUMMARIES TO SYNTHESIZE:
                        {combinedPartials}";

        return await SendSammaryPromt(prompt);
    }

    public async Task<string> TemplateSummariesAsync(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        // Stitching summaries together with the meeting agenda for context
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);

        string langInstruction = GetLanguageInstruction(langCode);

        // Making a prompt for company tenplate summaries
        string prompt = $@"You are a professional meeting minutes assistant.

                        {langInstruction}

                        INITIAL CONTEXT (Agenda): {meetingAgenda}
    
                        SUMMARIES OF SEGMENTS TO BE PROCESSED:
                        {combinedPartials}

                        TASK: Based on the following partial summaries from different segments of the meeting, create a comprehensive and professional final protocol strictly following this structure            

                        (Important: All headings in the final result must be in the same language as the main text)
                        STRUCTURE: 
                        # VERBALE DI RIUNIONE: [Meeting Name]
    
                        ## 1. Informazioni dalla Direzione
                        (Riassumi qui le comunicazioni, gli annunci e le direttive provenienti dai vertici o dalla direzione)

                        ## 2. Parte Gestionale
                        (Riassumi le decisioni riguardanti l'organizzazione, le risorse umane, i budget o i processi interni)

                        ## 3. Parte Operativa
                        (Riassumi i dettagli tecnici, lo stato di avanzamento dei progetti e le attività pratiche discusse)

                        ## 4. Eventuali
                        (Riassumi varie ed eventuali, comunicazioni minori o punti sollevati alla fine della riunione)
                        ";

        return await SendSammaryPromt(prompt);
    }

    private string GetLanguageInstruction(string langCode)
    {
        if (string.IsNullOrEmpty(langCode) || langCode == "auto")
            return "Detect the meeting language and write the summary in that language.";

        string langName = langCode switch
        {
            "it" => "ITALIAN",
            "ru" => "RUSSIAN",
            "en" => "ENGLISH",
            "de" => "GERMAN",
            "fr" => "FRENCH",
            "es" => "Spanish",
            "ua" => "Ukrainian",
            _ => langCode // For other languages, just return the code
        };

        return $"IMPORTANT: The entire summary, including all headings and bullet points, MUST be written in {langName}.";
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

            // Извлекаем чистый текст из ответа (здесь парсинг проще, так как это не JSON-режим)
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