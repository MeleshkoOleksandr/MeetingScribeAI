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
        return $@"You are a professional meeting assistant specializing in verbatim transcript post-processing.
                IMPORTANT: You are processing a SEGMENT of a larger meeting. 
                CONTEXT: {context}
                LIST OF EXPECTED PARTICIPANTS: {participants}

                TASK:
                1. CLEANUP (STRICT VERBATIM): Remove filler words ('umm', 'uh', 'like', 'sì', 'ecco', 'diciamo così'), stuttering, and accidental word repetitions (e.g., if a speaker says 'che è un po\' di potenza, che è un po\' di potenza', keep it only once). Fix obvious speech-to-text typos in technical terms. 
                   CRITICAL: DO NOT paraphrase, DO NOT summarize the text inside the 'Text' field, DO NOT improve grammar if it changes the speaker's original phrasing, and DO NOT use sophisticated vocabulary. Keep the original words, word order, and spoken style exactly as they are.

                2. DIARIZATION: Identify speakers. Based on the conversation context and the LIST OF EXPECTED PARTICIPANTS, identify who is speaking.
                   - If you are absolutely sure of the name, use the full name from the list.
                   - If you are unsure, use 'Speaker 1', 'Speaker 2', etc.
                   - DO NOT invent names that are not in the list or the transcript.

                3. CONSOLIDATE: The raw transcript is fragmented into very short time-coded lines. You MUST MERGE consecutive lines from the same speaker into single, long paragraphs. 
                   CRITICAL: 'Merging' means simply gluing the original text pieces together into a continuous text block after doing the CLEANUP. Do not rewrite the sentence structures during consolidation.
                   Only create a new JSON object when the speaker changes or there is a significant pause/topic shift.

                4. TIMESTAMPS: Use the timestamp of the FIRST segment of the merged block as the 'Timestamp' for that block.

                5. SUMMARIZING (FACT-PRESERVING DIGEST): Create a comprehensive, dense but complete summary of THIS segment specifically optimized for later consolidation into a final meeting summary.
                   CRITICAL: Do not generalize. You must preserve:
                    a. PRESERVE ALL DATA & METRICS:
                       - Extract exact numbers, KPIs, percentages, and metrics.
                       - ALWAYS capture context and comparisons if present (e.g., ""2025 vs 2026"", ""drop caused by X impacting Y"").
                    b. DECISIONS & ACTION ITEMS (THE ""GOLDEN TRIANGLE""):
                       - Explicitly highlight: WHAT must be done (Action) + WHO is responsible (Role/Name: e.g., CR, SM, CF) + WHEN (Deadline/Date).
                       - Never extract an action without looking for its owner and deadline in the text.
                    c. FEEDBACK & DIRECTIVES (Direction / UMA / Client):
                       - Separate external/management feedback into structured operational instructions (What needs to be done, what changes, constraints).
                    d. TOOLS & PROCESSES:
                       - Explicitly log mentions of systems, tools, and internal procedures (e.g., Hyper, Job Room, PCI). Record what to do, how, and when.
                    e. ELIMINATE CHITCHAT & VAGUE STATEMENTS:
                       - Filter out vague statements like ""things are going well"" or general discussions without consequences.
                       - Focus exclusively on: Findings / Operational Implications / What Changes / Action Required.
                
                6. Do not translate the meeting transcription. The result and summary must be in the same language as the input data. 

                STRICT CONSTRAINT:
                The 'Text' field must contain the EXACT spoken words of the speaker (minus fillers/duplications). It must remain in the original language (Italian in this case). Never turn spoken, slightly chaotic speech into formal written business prose.

                RETURN FORMAT (Strict JSON):
                {{
                  ""lines"": [
                    {{ ""Timestamp"": ""[00:00:00]"", ""SpeakerName"": ""Name"", ""Text"": ""..."" }}
                  ],
                  ""segmentSummary"": ""Summary of what happened in this part...""
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
        string prompt = $@"You are a professional executive meeting minutes assistant.

                        {langInstruction}

                        INITIAL CONTEXT (Agenda / Ordine del Giorno):
                        {meetingAgenda}

                        SUMMARIES OF SEGMENTS TO BE PROCESSED:
                        {combinedPartials}

                        TASK:
                        Based on the agenda and the partial segment summaries, generate a comprehensive meeting protocol in Markdown strictly using the 4 main sections below.

                        AGENDA MAPPING RULE:
                        - Map topics/points (Trattande) from the Agenda ({{meetingAgenda}}) into the corresponding section (1, 2, 3, or 4).
                        - For example, if a specific topic or decision (e.g. review procedure/escaletta, Sarah sync) belongs to ""Parte operativa"", place its full summary, actions, responsible person, and deadline under ""## 3. Parte Operativa"".

                        VADEMECUM QUALITY RULES FOR CONTENTS:
                        1. NO VAGUENESS: Transform descriptions into clear operational instructions (what changes, action required).
                        2. CONTEXTUALIZED DATA: Format metrics as [Data + Comparison] -> [Cause / Impact].
                        3. DECISION TRIAD: Every decision/action MUST specify:
                           - Action item (☐ Cosa fare)
                           - Responsabile (Name/Role/Sigla: e.g. CR, SM, CF). If missing, write ""Da definire"".
                           - Scadenza (Deadline). If missing, write ""Da definire"".
                        4. LANGUAGE: Keep headings exactly as defined below. The generated content under headings must match the required target language.

                        STRICT MARKDOWN STRUCTURE (Output MUST contain EXACTLY these 4 level-2 headings):

                        ## 1. Informazioni dalla Direzione
                        Tema: [Nome del tema]
                        Sintesi: [Sintesi chiara di cosa emerge]
                        Indicazioni operative:
                        • ☐ [Cosa deve essere fatto / cosa cambia]
                        Responsabile: [Nome / Sigla / Da definire]
                        Scadenza: [Data / Da definire]

                        ## 2. Parte Gestionale
                        Tema: [Nome del tema]
                        Decisione / Modalità operativa:
                        • ☐ [Azione definita o processo interno]
                        Responsabile: [Nome / Sigla / Da definire]
                        Scadenza: [Data / Da definire]

                        ## 3. Parte Operativa
                        Tema: [Nome del tema / Trattanda dall'Agenda]
                        Dati / Dettagli tecnici: [Numeri/Confronto 2025 vs 2026 oppure dettagli tecnici/strumenti]
                        Lettura del dato / Impatto: [Spiegazione causa/impatto o contesto]
                        Azioni operative:
                        • ☐ [Cosa fare, come farlo, quando]
                        Responsabile: [Nome / Sigla / Da definire]
                        Scadenza: [Data / Da definire]

                        ## 4. Eventuali
                        (Inserire qui solo comunicazioni minori o punti sollevati alla fine. Se non ve ne sono, scrivere: ""Nessun punto da segnalare"")
                        ";

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
