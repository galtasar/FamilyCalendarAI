using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FuzzySharp;
using Microsoft.Extensions.Logging;

namespace FamilyCalendar.Infrastructure.Services;

public class DuplicateDetectionService(IEventRepository eventRepo, ILogger<DuplicateDetectionService> logger) : IDuplicateDetectionService
{
    private const int FuzzyTitleThreshold = 85;

    public async Task<CalendarEvent?> FindMatchAsync(CalendarEvent candidate, CancellationToken ct = default)
    {
        var from = candidate.StartTime.Date.AddDays(-1);
        var to = candidate.StartTime.Date.AddDays(1);

        // Query separately for each family member so "Vera, Tage" matches events
        // stored individually as "Vera" or "Tage" as well as the joined form.
        var members = candidate.FamilyMemberName
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var seen = new HashSet<Guid>();
        foreach (var member in members)
        {
            var memberEvents = await eventRepo.GetByDateRangeAsync(
                new DateTimeOffset(from, TimeSpan.Zero),
                new DateTimeOffset(to.AddDays(1), TimeSpan.Zero),
                member, ct);

            foreach (var evt in memberEvents)
            {
                if (!seen.Add(evt.Id)) continue; // deduplicate
                if (IsMatch(candidate, evt))
                {
                    logger.LogInformation("Match found: '{Candidate}' matches existing '{Existing}'", candidate.Title, evt.Title);
                    return evt;
                }
            }
        }

        return null;
    }

    private static bool IsMatch(CalendarEvent candidate, CalendarEvent existing)
    {
        if (candidate.FamilyMemberName != existing.FamilyMemberName) return false;

        var dateDiff = Math.Abs((candidate.StartTime - existing.StartTime).TotalDays);
        if (dateDiff > 1) return false;

        var titleScore = Fuzz.Ratio(candidate.Title.ToLower(), existing.Title.ToLower());
        if (titleScore < FuzzyTitleThreshold) return false;

        return true;
    }
}
