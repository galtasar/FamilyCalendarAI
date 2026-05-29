using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IDuplicateDetectionService
{
    /// <summary>
    /// Returns the existing event that matches the candidate (same family member, same day, similar title),
    /// or null if no match is found.
    /// </summary>
    Task<CalendarEvent?> FindMatchAsync(CalendarEvent candidate, CancellationToken ct = default);
}
