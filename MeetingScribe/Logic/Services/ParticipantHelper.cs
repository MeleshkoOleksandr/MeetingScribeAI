using MeetingScribe.Logic.Meeting;
using System;
using System.Collections.Generic;
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
        // 1. Load all data (both people and groups)
        var (allPeople, allGroups) = TeamStorageService.LoadData();
        // 2.  Create a list of those PRESENTS
        var attendeeStrings = session.Participants.Select(p => $"{p.Name} ({p.Alias})");
        string attendeesResult = string.Join("; ", attendeeStrings);
        // Creating a set of IDs for quick searches
        var attendeeIds = session.Participants.Select(p => p.Id).ToHashSet();
        // 3. Determine the "Search Circle" for absentees
        IEnumerable<Participant> referenceList;

        if (!string.IsNullOrWhiteSpace(session.Team))
        {
            // If a team is specified, we search for its object to get the ID
            var targetGroup = allGroups.FirstOrDefault(g =>
                g.Name.Equals(session.Team, StringComparison.OrdinalIgnoreCase));

            if (targetGroup != null)
            {
                // If the team is found, we take only its members
                referenceList = allPeople.Where(p => p.GroupIds.Contains(targetGroup.Id));
            }
            else
            {
                // If the team name is specified but not found in the database — we take all people
                referenceList = allPeople;
            }
        }
        else
        {
            // If no team is specified — we take all people from the database
            referenceList = allPeople;
        }

        // 4. Form the string of ABSENT EES from the "Search Circle"
        var absentStrings = referenceList
            .Where(p => !attendeeIds.Contains(p.Id))
            .Select(p => $"{p.Name} ({p.Alias})");

        string absenteesResult = string.Join("; ", absentStrings);

        return (attendeesResult, absenteesResult);
    }
}