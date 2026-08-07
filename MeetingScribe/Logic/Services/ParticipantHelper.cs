using MeetingScribe.Logic.Meeting;
using System.Linq;
using System.Text;

namespace MeetingScribe.Logic.Services;

public static class ParticipantHelper
{
    /// <summary>
    /// Returns two lists: those present and those absent, in the format "Name (Alias);"
    /// </summary>
    public static (string Attendees, string Absentees) GetFormattedParticipantLists(MeetingSession session)
    {
        var (allPeople, _) = TeamStorageService.LoadData();

        // those who are present
        var attendeeStrings = session.Participants
            .Select(p => $"{p.Name} ({p.Alias})");

        string attendeesResult = string.Join("; ", attendeeStrings);

        // those who are absent
        var attendeeIds = session.Participants.Select(p => p.Id).ToHashSet();

        var absentStrings = allPeople
            .Where(p => !attendeeIds.Contains(p.Id))
            .Select(p => $"{p.Name} ({p.Alias})");

        string absenteesResult = string.Join("; ", absentStrings);

        return (attendeesResult, absenteesResult);
    }
}