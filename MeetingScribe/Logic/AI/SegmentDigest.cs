using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MeetingScribe.Logic.AI;

public class SegmentDigest
{
    // Discussed topics with details
    [JsonPropertyName("topicsDiscussed")]
    public List<TopicItem> TopicsDiscussed { get; set; } = new();

    // Metrics and data
    [JsonPropertyName("metricsAndData")]
    public List<string> MetricsAndData { get; set; } = new();

    // Action items and decisions
    [JsonPropertyName("actionItemsAndDecisions")]
    public List<ActionItem> ActionItemsAndDecisions { get; set; } = new();

    // Unresolved points
    [JsonPropertyName("unresolvedPoints")]
    public List<string> UnresolvedPoints { get; set; } = new();

    /// <summary>
    /// This method converts a structured object back into text.
    /// This is necessary to include this data in the final request to the AI for protocol concatenation.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (TopicsDiscussed.Count > 0)
        {
            sb.AppendLine("### Topics:");
            foreach (var item in TopicsDiscussed)
                sb.AppendLine($"- **{item.Topic}**: {item.Details}");
        }

        if (MetricsAndData.Count > 0)
        {
            sb.AppendLine("\n### Metrics:");
            foreach (var m in MetricsAndData) sb.AppendLine($"- {m}");
        }

        if (ActionItemsAndDecisions.Count > 0)
        {
            sb.AppendLine("\n### Actions:");
            foreach (var a in ActionItemsAndDecisions)
                sb.AppendLine($"- [ ] {a.Action} (Owner: {a.Owner}, Deadline: {a.Deadline})");
        }

        return sb.ToString();
    }
}

// Auxiliary class for discussed topics
public class TopicItem
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = ""; // Title of the topic (e.g., "Budget for marketing")

    [JsonPropertyName("details")]
    public string Details { get; set; } = ""; // Essence of the discussion (arguments, reasons, context)
}

// Auxiliary class for Action Items
public class ActionItem
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";
    [JsonPropertyName("deadline")]
    public string Deadline { get; set; } = "";
}