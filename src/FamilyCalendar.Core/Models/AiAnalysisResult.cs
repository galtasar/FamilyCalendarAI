using System.Text.Json.Serialization;

namespace FamilyCalendar.Core.Models;

public class AiAnalysisResult
{
    [JsonPropertyName("relevant")] public bool Relevant { get; set; }
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    [JsonPropertyName("children")] public List<string> FamilyMembers { get; set; } = [];
    [JsonPropertyName("event_type")] public string? EventType { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("start")] public DateTimeOffset? Start { get; set; }
    [JsonPropertyName("end")] public DateTimeOffset? End { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("requires_calendar_event")] public bool RequiresCalendarEvent { get; set; }
    [JsonPropertyName("requires_manual_review")] public bool RequiresManualReview { get; set; }
    [JsonPropertyName("is_recurring")] public bool IsRecurring { get; set; }
    [JsonPropertyName("has_time")] public bool HasTime { get; set; }
    [JsonPropertyName("summary")] public string? Summary { get; set; }
}
