using MeetingScribe.Logic.Meeting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public interface IAiService
{
    Task<AiResponseChunk?> ProcessChunkAsync(string rawText, string participants, string context);
    Task<string> StitchSummariesAsync(List<string> partialSummaries, string meetingAgenda);
}