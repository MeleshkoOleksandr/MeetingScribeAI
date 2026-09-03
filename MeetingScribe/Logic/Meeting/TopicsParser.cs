using MeetingScribe.Logic.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MeetingScribe.Logic.Meeting;

public class AgendaTopic
{
    public string Title { get; set; }
}

public class AgendaSection
{
    public string Number { get; set; }
    public string Title { get; set; }
    public List<AgendaTopic> Topics { get; set; } = new List<AgendaTopic>();
}

public static class TopicsParser
{
    public static List<AgendaSection> Parse(string agendaText)
    {
        try
        {
            var sections = new List<AgendaSection>();
            if (string.IsNullOrWhiteSpace(agendaText))
                return sections;

            var lines = agendaText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            AgendaSection currentSection = null;

            // Formatting Rules for Sections (Heading) 
            var sectionRegex = new Regex(@"^\s*(\d+)\.\s*(.+)$");
            var topicRegex = new Regex(@"^\s*[●•\-\*Ø\d\.]+\s*(.+)$");

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Skipping the header "Ordine del giorno:"
                if (line.StartsWith("Ordine del giorno", StringComparison.OrdinalIgnoreCase))
                    continue;

                var sectionMatch = sectionRegex.Match(line);
                if (sectionMatch.Success)
                {
                    currentSection = new AgendaSection
                    {
                        Number = sectionMatch.Groups[1].Value,
                        Title = sectionMatch.Groups[2].Value.Trim()
                    };
                    sections.Add(currentSection);
                    continue;
                }

                if (currentSection != null)
                {
                    var topicMatch = topicRegex.Match(line);
                    if (topicMatch.Success)
                    {
                        currentSection.Topics.Add(new AgendaTopic
                        {
                            Title = topicMatch.Groups[1].Value.Trim()
                        });
                    }
                }
            }
            return sections;
        }
        catch (Exception ex)
        {
            LogService.Instance.LogException(ex, "Error parsing agenda");
            return null;
        }

    }

    public static string BuildExpectedMarkdownSkeleton(List<AgendaSection> sections)
    {
        var sb = new StringBuilder();
        foreach (var sec in sections)
        {
            
            if (sec.Topics.Count > 0)
            {
                sb.AppendLine($"## {sec.Number}. {sec.Title}");
                foreach (var topic in sec.Topics)
                {             
                    sb.AppendLine($"### {topic.Title}");
                    sb.AppendLine("[Fill with discussion summary, context, metrics, directives]");
                    sb.AppendLine("**Azioni / Decisioni:**");
                    sb.AppendLine("• [Action] | **Resp:** [Person/Role] | **Scadenza:** [Date/Condition]");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }
}