namespace MeetingScribe.Logic.AI;

public static class AiServiceFactory
{
    public static IAiService? Create(AiProvider? config, string apiKey)
    {
        if (config == null || string.IsNullOrEmpty(apiKey)) return null;

        return config.Id switch
        {
            "Gemini_Free" => new GeminiAiService(apiKey, config.Model, config.Url, isPaid: false),
            "Gemini_Paid" => new GeminiAiService(apiKey, config.Model, config.Url, isPaid: true),
            "ChatGPT" => new ChatGptService(apiKey, config.Model, config.Url),
            _ => null
        };
    }
}