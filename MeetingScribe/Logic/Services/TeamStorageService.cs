using MeetingScribe.Logic.Meeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MeetingScribe.Logic.Services;

public static class TeamStorageService
{
    private static readonly string FolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Data");
    private static readonly string ParticipantsPath = Path.Combine(FolderPath, "participants.json");
    private static readonly string GroupsPath = Path.Combine(FolderPath, "groups.json");

    public static void SaveData(IEnumerable<Participant> people, IEnumerable<TeamGroup> groups)
    {
        Directory.CreateDirectory(FolderPath);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ParticipantsPath, JsonSerializer.Serialize(people, options));
        File.WriteAllText(GroupsPath, JsonSerializer.Serialize(groups, options));
    }

    public static (List<Participant>, List<TeamGroup>) LoadData()
    {
        if (!Directory.Exists(FolderPath)) return (new(), new());

        var people = File.Exists(ParticipantsPath)
            ? JsonSerializer.Deserialize<List<Participant>>(File.ReadAllText(ParticipantsPath))
            : new List<Participant>();

        var groups = File.Exists(GroupsPath)
            ? JsonSerializer.Deserialize<List<TeamGroup>>(File.ReadAllText(GroupsPath))
            : new List<TeamGroup>();

        return (people ?? new(), groups ?? new());
    }
}