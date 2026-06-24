using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MeetingScribe.Logic.Meeting;

public partial class MeetingSession : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FolderPath { get; set; } = "";


    public string Language { get; set; } = "en";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _meetingTopics = string.Empty;


    public DateTime StartTime { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; }


    public ObservableCollection<TranscriptLine> FullTranscript { get; set; } = new();   // Full transcription
    public List<SpeakerParticipant> Participants { get; set; } = new();
    [ObservableProperty] private string? _aiSummary;


    public bool hasTranscription => FullTranscript.Count > 0;
    public bool hasSummary => !string.IsNullOrEmpty(AiSummary);
}