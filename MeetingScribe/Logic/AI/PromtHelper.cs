using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public static class PromtHelper
{
    private static readonly Dictionary<string, string> _prompts = new();

    static PromtHelper()
    {
        LoadPrompts();
    }

    public static void LoadPrompts()
    {
        try
        {
            _prompts.Clear();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "prompts.md");
            if (!File.Exists(path))
            {
                return;
            }

            string[] lines = File.ReadAllLines(path);
            string currentHeader = null;
            var currentContent = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("# "))
                {
                    if (currentHeader != null)
                    {
                        _prompts[currentHeader] = currentContent.ToString().Trim();
                        currentContent.Clear();
                    }
                    currentHeader = line.Substring(2).Trim();
                }
                else if (currentHeader != null)
                {
                    string cleanLine = line;
                    
                    if (cleanLine.TrimStart().StartsWith("//"))
                    {
                        continue; // Skip comment lines entirely
                    }

                    int commentIdx = cleanLine.IndexOf(" // ");
                    if (commentIdx >= 0)
                    {
                        // Strip inline comment but keep the line
                        cleanLine = cleanLine.Substring(0, commentIdx);
                    }

                    currentContent.AppendLine(cleanLine);
                }
            }

            if (currentHeader != null)
            {
                _prompts[currentHeader] = currentContent.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            // Optional: Log the exception somewhere if a logger is available
            Console.WriteLine($"Error loading prompts: {ex.Message}");
        }
    }

    public static string GetPrompt(string key)
    {
        return _prompts.TryGetValue(key, out var val) ? val : $"[Prompt {key} not found]";
    }

    public static string WisperInitialPrompt(MeetingSession session)
    {
        var (present, absent) = ParticipantHelper.GetFormattedParticipantLists(session);
        return string.Format(GetPrompt("WisperInitialPrompt"), 
            session.Description, 
            session.Team, 
            present, 
            absent, 
            session.MeetingTopics, 
            KeywordsPrompt());
    }

    public static string KeywordsPrompt()
    {
        return string.Format(GetPrompt("KeywordsPrompt"), Acronyms(), Nicknames());
    }

    public static string Nicknames()
    {
        return GetPrompt("Nicknames");
    }

    public static string Acronyms()
    {
        return GetPrompt("Acronyms");
    }

    public static string BuildCombinedPrompt(string raw, string participants, string context)
    {
        return string.Format(GetPrompt("BuildCombinedPrompt"), context, participants, Acronyms(), raw);
    }

    public static string PartialSummaryPrompt(string raw, string participants, string context, string langCode)
    {
        return string.Format(GetPrompt("PartialSummaryPrompt"), context, participants, GetLanguageInstruction(langCode), raw);
    }

    public static string GeneralSummariesPromt(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);
        string langInstruction = GetLanguageInstruction(langCode);

        return string.Format(GetPrompt("GeneralSummariesPrompt"), langInstruction, meetingAgenda, combinedPartials);
    }

    public static string TemplateSummariesPromt(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);
        string langInstruction = GetLanguageInstruction(langCode);

        var sections = TopicsParser.Parse(meetingAgenda);
        string expectedStructure = TopicsParser.BuildExpectedMarkdownSkeleton(sections);

        return string.Format(GetPrompt("TemplateSummariesPrompt"), langInstruction, Acronyms(), Nicknames(), combinedPartials, expectedStructure);
    }

    private static string GetLanguageInstruction(string langCode)
    {
        if (string.IsNullOrEmpty(langCode) || langCode == "auto")
            return "Detect the meeting language and write the summary in that language.";

        string langName = langCode switch
        {
            "it" => "ITALIAN",
            "ru" => "RUSSIAN",
            "en" => "ENGLISH",
            "de" => "GERMAN",
            "fr" => "FRENCH",
            "es" => "Spanish",
            "ua" => "Ukrainian",
            _ => langCode // For other languages, just return the code
        };

        return $"LANGUAGE: The entire summary, including all headings and bullet points, MUST be written in {langName}.";
    }
}
