
namespace MeetingScribe.Logic.AI;

public class AiProvider
{
    public string Id { get; set; } = string.Empty;    // "Gemini" or "ChatGPT"
    public string Name { get; set; } = string.Empty;  // "Google Gemini"
    public string Url { get; set; } = string.Empty;   // API Endpoint
    public string Model { get; set; } = string.Empty; // Model name (e.g. gemini-1.5-flash)
}