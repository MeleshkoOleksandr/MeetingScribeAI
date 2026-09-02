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
    [JsonPropertyName("segmentDigest")]
    public SegmentDigest? SegmentDigest { get; set; }

    //For compatibility with the old code, we will return the segment summary as a string
    public string SegmentSummary => SegmentDigest?.ToString() ?? "";
}