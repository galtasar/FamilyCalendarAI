using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IDuplicateDetectionService
{
    Task<bool> IsDuplicateAsync(CalendarEvent candidate, CancellationToken ct = default);
}
