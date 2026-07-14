using MeetingScribe.Logic.Meeting;
using System.Collections.Generic;

namespace MeetingScribe.Logic.AI;

public class AiResponseChunk
{
    // List of refined transcript lines returned by the AI for this chunk
    public List<TranscriptLine> Lines { get; set; } = new();
    // Short summary of the chunk, if provided by the AI
    public string SegmentSummary { get; set; } = "";
}