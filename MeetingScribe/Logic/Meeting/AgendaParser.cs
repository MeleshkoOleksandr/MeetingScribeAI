using System;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;


namespace MeetingScribe.Logic.Meeting;

public class AgendaParser
{
    /// <summary>
    /// Parses a docx agenda file and returns participants and topics as two flat strings. 
    /// This class is writen to handle the specific structure of the agenda template in italian, which includes sections for participants, absentees, and a detailed agenda with technical subheaders.
    /// </summary>
    /// <param name="filePath">The path to the .docx file.</param>
    /// <returns>A tuple containing (Participants, Topics).</returns>
    public static (string Participants, string Topics) ParseMeetingAgenda(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var participantsBuilder = new StringBuilder();
        var topicsBuilder = new StringBuilder();

        // Open the docx document in read-only mode (false)
        using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return (string.Empty, string.Empty);

            // Get all paragraphs from the document body
            var paragraphs = body.Elements<Paragraph>().ToList();

            bool readingParticipants = false;
            bool readingTopics = false;

            foreach (var p in paragraphs)
            {
                // Extract clean text from the paragraph
                string text = p.InnerText?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;

                // --- 1. Logic for Participants ---
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

                // --- 2. Logic for Topics (Agenda) ---
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
                        text.StartsWith("Informazioni dalla Direzione", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Eventuali", StringComparison.OrdinalIgnoreCase))
                    { 
                        topicsBuilder.AppendLine(" - " + text);
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

        return (finalParticipants, finalTopics);
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