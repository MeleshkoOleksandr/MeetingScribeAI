using MeetingScribe.Logic.Meeting;
using MeetingScribe.Logic.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MeetingScribe.Logic.AI;

public static class PromtHelper
{
    public static string WisperInitialPrompt(MeetingSession session)
    {

        var (present, absent) = ParticipantHelper.GetFormattedParticipantLists(session);

        return $@"You are a professional meeting assistant specializing in verbatim transcript post-processing.
        
                DESCRIPTION: {session.Description}
                TEAM NAME: {session.Team}
                PRESENT PERSON NAMES: {present}
                Absent PERSON NAMES: {absent}
                MEETING TOPICS: {session.MeetingTopics}

                {WisperKeywordsPrompt()}";
    }

    public static string WisperKeywordsPrompt()
    {
        return $@"POSIBLE KEYWORDS:
                    Internal Acronyms & Projects: PCI, WS, CF, CR, CP, URC, UMA, PML, LT, GeZ, ConvoGeZ, Labor Transfer, TRI Form, TRI Coaching, Team AtelierTRI
                    Tools & Software: Hyper, ChatGPT Business, Synthesia Creator, IA, AI, QR code, WhatsApp, Teams
                    Domain Terminology (Italian context): formatori, collaboratori specialisti, supervisioni in aula, discenti, colloquio di consulenza, colloquio di aggancio, progettazione didattica, bisogno formativo, scelta metodologica, assessment, esercizio, autoapprendimento, trattande, verbale
            ";
    }

    public static string BuildCombinedPrompt(string raw, string participants, string context)
    {
        return $@"You are an expert meeting processing system specialized in:
                1) Verbatim speech-to-text post-processing and diarization.
                2) High-density, structured intermediate summarization.

                INPUT CONTEXT:
                - Segment Context: {context}
                - Expected Participants: {participants}

                TASK 1: TRANSCRIPT REFINEMENT & MERGING
                - Clean: Remove filler words (e.g., 'umm', 'uh', 'like', 'sì', 'ecco', 'diciamo così'), stutters, and accidental duplicate words/phrases. Fix obvious STT transcription typos in technical names.
                - Diarize: Match speakers to the EXPECTED PARTICIPANTS list. If uncertain, use 'Speaker 1', 'Speaker 2', etc. Do not invent names.
                - Consolidate: Merge consecutive fragmented lines from the same speaker into coherent, continuous blocks. Keep the timestamp of the first segment in the merged block.
                - Absolute Constraint: In the 'text' field, DO NOT summarize, paraphrase, or rewrite sentence structures. Retain the speaker's original spoken phrasing, language, and lexical style.

                TASK 2: DETAILED INTERMEDIATE SEGMENT DIGEST
                Generate an exhaustive, highly specific structured digest of THIS segment. This digest will be programmatically aggregated into a global meeting summary.
                - Depth & Coverage: Do not abstract or compress multiple discussion points into broad generalizations. Detail each distinct topic with its underlying causes, arguments, and operational context.
                - Key Metrics & Data: Record all exact numbers, dates, targets, percentages, and year-over-year/period comparisons.
                - Action Items & Decisions: Extract full triples: Action + Assignee/Owner + Deadline/Condition. Note if an owner or deadline was discussed but left undefined.
                - Systems & Workflows: Log specific tools, software, directives, or procedural constraints mentioned.
                - Open / Unresolved Points: Note any topic left open or requiring follow-up in subsequent segments.

                LANGUAGE & FORMAT CONSTRAINTS:
                - Output MUST be strictly valid JSON.
                - Language: Keep both transcript text and summary data in the exact same language as the input audio/transcript.

                OUTPUT SCHEMA:
                    {{
                      ""lines"": [
                        {{ ""Timestamp"": ""[00:00:00]"", ""SpeakerName"": ""Name"", ""Text"": ""..."" }}
                      ],
                    ""segmentDigest"": {{
                    ""topicsDiscussed"": [
                      {{
                        ""topic"": ""Topic title"",
                        ""details"": ""In-depth breakdown of what was stated, key arguments, causes, and operational context.""
                      }}
                    ],
                    ""metricsAndData"": [
                      ""Exact metric, figure, comparison, or percentage with its context""
                    ],
                    ""actionItemsAndDecisions"": [
                      {{
                        ""action"": ""Specific task or decision made"",
                        ""owner"": ""Responsible person/role or 'Unassigned'"",
                        ""deadline"": ""Due date, milestone, or 'Not specified'""
                      }}
                    ],
                    ""toolsAndProcedures"": [
                      ""Mentioned tool, system (e.g., Hyper, Job Room, PCI), or procedure and how it applies""
                    ],
                    ""unresolvedPoints"": [
                      ""Open questions, pending verifications, or interrupted topics""
                    ]
                  }}
                }}

                RAW DATA:
                {raw}";
    }

    public static string GeneralSummariesPromt(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        // Stitching summaries together with the meeting agenda for context
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);

        string langInstruction = GetLanguageInstruction(langCode);

        // Making a prompt for the AI to make summaries
        string prompt = $@"You are a professional meeting minutes assistant.

                        {langInstruction}

                        TASK: Based on the following partial summaries from different segments of the meeting, create a comprehensive and professional final protocol in Markdown format.
                                     
                        AGENDA CONTEXT: {meetingAgenda}
    
                        (Important: All headings in the final result must be in the same language as the main text)
                        STRUCTURE: 
                        # [Meeting Name]
                        ## Executive Summary (3-6 powerful sentences)
                        ## Key Discussion Points & Decisions
                        ## Action Items (Format as: Task | Assigned to | Deadline)
                        ## Next Steps

                        PARTIAL SUMMARIES TO SYNTHESIZE:
                        {combinedPartials}";

        return prompt;
    }

    public static string TemplateSummariesPromt(List<string> partialSummaries, string meetingAgenda, string langCode)
    {
        // Stitching summaries together with the meeting agenda for context
        string combinedPartials = string.Join("\n\n---\n\n", partialSummaries);

        string langInstruction = GetLanguageInstruction(langCode);

        // Making a prompt for company tenplate summaries
        var sections = TopicsParser.Parse(meetingAgenda);
        string expectedStructure = TopicsParser.BuildExpectedMarkdownSkeleton(sections);

        string prompt = $@"You are a professional executive meeting secretary.
                        {langInstruction}

                        INPUT DATA:
                        1. OFFICIAL AGENDA (Ordine del Giorno):
                        {meetingAgenda}

                        2. DETAILED SEGMENT SUMMARIES / DIGESTS:
                        {combinedPartials}

                        TARGET OUTPUT STRUCTURE (MANDATORY TEMPLATE):
                        You MUST follow this exact structure derived from the agenda. Do not remove or reorganize any section or subsection:
                        {expectedStructure}

                        CORE GENERATION RULES:
                        1. STRICT AGENDA ANCHORING:
                           - For every planned topic (### Topic), locate and compile all related information from the segment digests.
                           - If a planned agenda topic was NOT discussed in the meeting at all, write explicitly: *""Non trattato durante la riunione.""*

                        2. UNIFORM CONTENT SYNTHESIS (No Vague Statements):
                           - Under each topic, write clear, dense, and operational notes (context, changes, operational directives, guidelines, and reasons).
                           - Metrics & Data: Format figures with context (e.g., ""FK: 2 PCI, FP: 1 PCI, LP: 3 PCI (2 convocate con ConvoGeZ)"").
                           - Directives & Guidelines: If management/UMA requirements are discussed, list them as clear bullet points.

                        3. ACTION TRIAD FORMAT (Mandatory for all tasks/decisions):
                           Under each topic's `**Azioni / Decisioni:**` section, use this exact line format:
                           • ☐ [Specific Action / Task] | **Resp:** [Name/Role/Sigla or 'Da definire'] | **Scadenza:** [Date/Deadline or 'Da definire']
                           *(If no action was decided for that topic, write: • Nessuna azione specifica assegnata).*

                        4. UNPLANNED & EXTRA TOPICS:
                           - If topics emerged during the meeting that were NOT in the official agenda, place them under `## Eventuali` (or create a subsection `### Fuori Ordine del Giorno` inside the relevant section).

                        OUTPUT INSTRUCTIONS:
                        - Generate pure Markdown matching the TARGET OUTPUT STRUCTURE.
                        - Do not output introductory or concluding meta-commentary.";
        return prompt;
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

        return $"IMPORTANT: The entire summary, including all headings and bullet points, MUST be written in {langName}.";
    }

}
