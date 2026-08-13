using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MeetingScribe.Logic.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


namespace MeetingScribe.Logic.Meeting;

public class AgendaParser
{
    /// <summary>
    /// Parses a docx agenda file and returns participants and topics as two flat strings. 
    /// This class is writen to handle the specific structure of the agenda template in italian, which includes sections for participants, absentees, and a detailed agenda with technical subheaders.
    /// </summary>
    /// <param name="filePath">The path to the .docx file.</param>
    /// <returns>A tuple containing (Participants, Topics, Team, Date,Time, Venue).</returns>
    public static (string Participants, string Topics, string Team, string Date, string Time, string Venue) ParseMeetingAgenda(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var participantsBuilder = new StringBuilder();
        var topicsBuilder = new StringBuilder();

        string team = string.Empty;
        string date = string.Empty;
        string time = string.Empty;
        string venue = string.Empty;

        // Open the docx document in read-only mode (false)
        using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

            // Get all paragraphs from the document body
            var paragraphs = body.Elements<Paragraph>().ToList();

            bool readingParticipants = false;
            bool readingTopics = false;

            int counter = 1;
            foreach (var p in paragraphs)
            {
                // Extract clean text from the paragraph
                string text = p.InnerText?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;

                //Doc end condition: If we reach the "Prossima riunione" section, we stop reading further as it indicates the end of the relevant content.
                if (text.StartsWith("Prossima riunione", StringComparison.OrdinalIgnoreCase) && readingTopics)
                {
                    break;
                }
                // --- Logic for Team---
                if (text.StartsWith("TEAM", StringComparison.OrdinalIgnoreCase))
                {
                    team = text["TEAM ".Length..];
                }
                // --- Logic for Date and Place---
                if (text.Contains("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    // Regex Template to extract date, time, and venue from the text
                    string pattern = @"DATA:\s*(?<date>\d{2}\.\d{2}\.\d{4})\s*(?:ORE|–|-)?\s*(?<time>\d{1,2}\.\d{2}\s*[–-]\s*\d{1,2}\.\d{2})\s*LUOGO(?:-[A-Z]+)?[:–\s-]+\s*(?<venue>[^D]+)";
                    Match match = Regex.Match(text, pattern);

                    if (match.Success)
                    {
                        date = match.Groups["date"].Value;
                        time = match.Groups["time"].Value;
                        venue = match.Groups["venue"].Value.Trim();
                    }
                    else
                    {
                        LogService.Instance.Log("The data format did not match the expected format.", Enums.LogLevel.Warning);
                    }
                }

                // --- Logic for Participants ---
                if (text.StartsWith("Partecipanti:", StringComparison.OrdinalIgnoreCase))
                {
                    readingParticipants = true;
                    readingTopics = false;

                    // Extract names if they are on the same line right after "Partecipanti:"
                    string namesContent = text.Substring("Partecipanti:".Length).Trim();
                    if (!string.IsNullOrEmpty(namesContent))
                    {
                        participantsBuilder.Append(namesContent);
                    }
                    continue;
                }

                if (readingParticipants)
                {
                    // If we reach the absent section (Assenti) or the agenda section (Ordine del giorno), stop collecting participants
                    if (text.StartsWith("Assenti:", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Ordine del giorno:", StringComparison.OrdinalIgnoreCase))
                    {
                        readingParticipants = false;
                    }
                    else
                    {
                        // Append names (in case the participant list continues on a new line)
                        if (participantsBuilder.Length > 0 && !participantsBuilder.ToString().EndsWith(";"))
                        {
                            participantsBuilder.Append("; ");
                        }
                        participantsBuilder.Append(text);
                        continue;
                    }
                }

                // --- Logic for Topics (Agenda) ---
                if (text.StartsWith("Ordine del giorno:", StringComparison.OrdinalIgnoreCase))
                {
                    readingTopics = true;
                    continue;
                }

                if (readingTopics)
                {
                    // technical subheaders / sections, 
                    if (text.StartsWith("Parte operativa", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Parte gestionale", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Informazioni", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Eventuali", StringComparison.OrdinalIgnoreCase))
                    {
                        topicsBuilder.AppendLine(counter++.ToString() + ". " + text);
                        continue;
                    }
                    // Append topics to the string builder,
                    if (topicsBuilder.Length > 0)
                    {
                        topicsBuilder.AppendLine(text);
                    }
                }
            }
        }

        // Clean up the results by removing extra spaces and empty elements
        string finalParticipants = CleanUpResult(participantsBuilder.ToString());
        string finalTopics = CleanUpResult(topicsBuilder.ToString());

        return (finalParticipants, finalTopics, team, date, time, venue);
    }

    /// <summary>
    /// Helper method to clean up and consistently format strings using "; " as a separator.
    /// </summary>
    private static string CleanUpResult(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var items = input.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(i => i.Trim())
                         .Where(i => !string.IsNullOrEmpty(i));

        return string.Join("; ", items);
    }
}