using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingScribe.Logic.Meeting;

public class MeetingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public  string MeetingTopics { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; }

    // Full transcription history
    public List<TranscriptLine> FullTranscript { get; set; } = new();

    public string? AiSummary { get; set; }
    public List<SpeakerParticipant> Participants { get; set; } = new();
}