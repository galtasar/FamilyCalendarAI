namespace FamilyCalendar.Core.Models;

public class Email
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public Core.Enums.EmailClassification Classification { get; set; }
    public double? Confidence { get; set; }
    public string? RawAiResponse { get; set; }
    public ICollection<CalendarEvent> Events { get; set; } = [];
}
