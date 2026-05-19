using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IEventRepository
{
    Task<CalendarEvent> AddAsync(CalendarEvent calendarEvent, CancellationToken ct = default);
    Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken ct = default);
    Task<CalendarEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEvent>> GetPendingReviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEvent>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, string? childName = null, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEvent>> GetExpiredPendingAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEvent>> GetAllAsync(string? childName = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);
}
