using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IReviewNotificationService
{
    Task SendReviewNotificationAsync(CalendarEvent calendarEvent, CancellationToken ct = default);
}
