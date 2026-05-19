using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FuzzySharp;
using Microsoft.Extensions.Logging;

namespace FamilyCalendar.Infrastructure.Services;

public class DuplicateDetectionService(IEventRepository eventRepo, ILogger<DuplicateDetectionService> logger) : IDuplicateDetectionService
{
    private const int FuzzyTitleThreshold = 85;

    public async Task<bool> IsDuplicateAsync(CalendarEvent candidate, CancellationToken ct = default)
    {
        var from = candidate.StartTime.Date.AddDays(-1);
        var to = candidate.StartTime.Date.AddDays(1);

        var existing = await eventRepo.GetByDateRangeAsync(
            new DateTimeOffset(from, TimeSpan.Zero),
            new DateTimeOffset(to.AddDays(1), TimeSpan.Zero),
            candidate.FamilyMemberName, ct);

        foreach (var evt in existing)
        {
            if (IsDuplicate(candidate, evt))
            {
                logger.LogInformation("Duplicate found: {Candidate} matches {Existing}", candidate.Title, evt.Title);
                return true;
            }
        }

        return false;
    }

    private static bool IsDuplicate(CalendarEvent candidate, CalendarEvent existing)
    {
        if (candidate.FamilyMemberName != existing.FamilyMemberName) return false;

        var dateDiff = Math.Abs((candidate.StartTime - existing.StartTime).TotalDays);
        if (dateDiff > 1) return false;

        var titleScore = Fuzz.Ratio(candidate.Title.ToLower(), existing.Title.ToLower());
        if (titleScore < FuzzyTitleThreshold) return false;

        return true;
    }
}
