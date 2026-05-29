namespace FamilyCalendar.Core.Models;

public class CalendarEvent
{
    public Guid Id { get; set; }
    public Guid EmailId { get; set; }
    public Email Email { get; set; } = null!;
    public string FamilyMemberName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? Location { get; set; }
    public string CalendarProvider { get; set; } = "google";
    public string? CalendarEventId { get; set; }
    public string? ReviewQuestionsJson { get; set; }
    public Core.Enums.EventStatus Status { get; set; }
    public bool NeedsReview { get; set; }
    public bool HasTime { get; set; }
    public string? SyncError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
