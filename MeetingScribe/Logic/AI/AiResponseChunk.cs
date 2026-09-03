using MeetingScribe.Logic.Meeting;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MeetingScribe.Logic.AI;

public class AiResponseChunk
{
    // List of refined transcript lines returned by the AI for this chunk
    [JsonPropertyName("lines")]
    public List<TranscriptLine>? Lines { get; set; }
    // Short summary of the chunk, if provided by the AI
    [JsonPropertyName("segmentSummary")]
    public string? SegmentSummary { get; set; }
}