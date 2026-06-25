using MeetingScribe.Logic.Meeting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public interface IAiService
{
    Task<List<TranscriptLine>> RefineAndDiarizeAsync(
        string rawTranscript,
        string participants,
        string meetingContext);
}