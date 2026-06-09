using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyCalendar.Infrastructure.Repositories;

public class EventRepository(AppDbContext db) : IEventRepository
{
    public async Task<CalendarEvent> AddAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(ct);
        return calendarEvent;
    }

    public async Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        db.CalendarEvents.Update(calendarEvent);
        await db.SaveChangesAsync(ct);
    }

    public Task<CalendarEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.CalendarEvents.Include(e => e.Email).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<CalendarEvent>> GetPendingReviewAsync(CancellationToken ct = default) =>
        await db.CalendarEvents
            .Where(e => e.Status == EventStatus.Pending && e.NeedsReview)
            .OrderBy(e => e.StartTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CalendarEvent>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, string? familyMemberName = null, CancellationToken ct = default)
    {
        var q = db.CalendarEvents.Where(e => e.StartTime >= from && e.StartTime <= to);
        if (familyMemberName != null) q = ApplyFamilyMemberFilter(q, familyMemberName);
        return await q.OrderBy(e => e.StartTime).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetExpiredPendingAsync(DateTimeOffset now, CancellationToken ct = default) =>
        await db.CalendarEvents
            .Where(e => e.Status == EventStatus.Pending && e.NeedsReview && e.StartTime < now)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync(string? familyMemberName = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        var q = db.CalendarEvents.AsQueryable();
        if (familyMemberName != null) q = ApplyFamilyMemberFilter(q, familyMemberName);
        if (from != null) q = q.Where(e => e.StartTime >= from);
        if (to != null) q = q.Where(e => e.StartTime <= to);
        return await q.OrderBy(e => e.StartTime).ToListAsync(ct);
    }

    // Matches a single member name within a comma-separated FamilyMemberName column.
    // Works across EF Core providers (InMemory, SQLite, PostgreSQL).
    private static IQueryable<CalendarEvent> ApplyFamilyMemberFilter(IQueryable<CalendarEvent> q, string name) =>
        q.Where(e =>
            e.FamilyMemberName == name ||
            e.FamilyMemberName.StartsWith(name + ",") ||
            e.FamilyMemberName.EndsWith(", " + name) ||
            e.FamilyMemberName.Contains(", " + name + ","));
}
