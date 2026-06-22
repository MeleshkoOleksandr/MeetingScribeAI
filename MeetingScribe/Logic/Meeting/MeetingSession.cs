using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    public string FolderPath { get; set; } = "";

    public ObservableCollection<TranscriptLine> FullTranscript { get; set; } = new();   // Full transcription
    public List<SpeakerParticipant> Participants { get; set; } = new();
    public string? aiSummary { get; set; }

    public bool hasTranscription => FullTranscript.Count > 0;
    public bool hasSummary => !string.IsNullOrEmpty(aiSummary);

}