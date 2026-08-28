using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IReviewNotificationService
{
    Task SendReviewNotificationAsync(IReadOnlyList<CalendarEvent> calendarEvents, CancellationToken ct = default);
}
