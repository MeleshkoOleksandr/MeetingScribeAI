using MeetingScribe.Logic.Meeting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public interface IAiService
{
    Task<AiResponseChunk?> RefineTranscriptAsync(string rawText, string participants, string context, CancellationToken token);
    Task<AiResponseChunk?> MakeSummaryAsync(string rawText, string participants, string context, string langCode, CancellationToken token);
    Task<string> StitchSummariesAsync(List<string> partialSummaries, string meetingAgenda, string langCode);
    Task<string> TemplateSummariesAsync(List<string> partialSummaries, string meetingAgenda, string langCode);
}