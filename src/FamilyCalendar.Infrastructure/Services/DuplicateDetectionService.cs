using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FuzzySharp;
using Microsoft.Extensions.Logging;

namespace FamilyCalendar.Infrastructure.Services;

public class DuplicateDetectionService(IEventRepository eventRepo, ILogger<DuplicateDetectionService> logger) : IDuplicateDetectionService
{
    private const int FuzzyTitleThreshold = 85;
    private const double CrossMemberTimeThresholdHours = 2.0;

    public async Task<CalendarEvent?> FindMatchAsync(CalendarEvent candidate, CancellationToken ct = default)
    {
        var from = candidate.StartTime.Date.AddDays(-1);
        var to = candidate.StartTime.Date.AddDays(1);

        // Query separately for each family member so "Vera, Tage" matches events
        // stored individually as "Vera" or "Tage" as well as the joined form.
        var members = SplitMembers(candidate.FamilyMemberName);

        var seen = new HashSet<Guid>();
        foreach (var member in members)
        {
            var memberEvents = await eventRepo.GetByDateRangeAsync(
                new DateTimeOffset(from, TimeSpan.Zero),
                new DateTimeOffset(to.AddDays(1), TimeSpan.Zero),
                member, ct);

            foreach (var evt in memberEvents)
            {
                if (!seen.Add(evt.Id)) continue;
                if (IsMatch(candidate, evt))
                {
                    logger.LogInformation("Match found: '{Candidate}' matches existing '{Existing}'", candidate.Title, evt.Title);
                    return evt;
                }
            }
        }

        // Cross-member search: find same-day, same-title events for DIFFERENT family members.
        // This handles cases like "Vera: Skolavslutning" and "Tage: Skolavslutning" being
        // merged into one shared event rather than stored as two separate events.
        var allDayEvents = await eventRepo.GetByDateRangeAsync(
            new DateTimeOffset(from, TimeSpan.Zero),
            new DateTimeOffset(to.AddDays(1), TimeSpan.Zero),
            null, ct);

        foreach (var evt in allDayEvents)
        {
            if (!seen.Add(evt.Id)) continue;
            if (IsCrossMemberMatch(candidate, evt))
            {
                logger.LogInformation(
                    "Cross-member match: '{Candidate}' ({CandidateMember}) matches '{Existing}' ({ExistingMember})",
                    candidate.Title, candidate.FamilyMemberName, evt.Title, evt.FamilyMemberName);
                return evt;
            }
        }

        return null;
    }

    private static bool IsMatch(CalendarEvent candidate, CalendarEvent existing)
    {
        var candidateMembers = SplitMembers(candidate.FamilyMemberName);
        var existingMembers = SplitMembers(existing.FamilyMemberName);
        if (!candidateMembers.Intersect(existingMembers, StringComparer.OrdinalIgnoreCase).Any())
            return false;

        var dateDiff = Math.Abs((candidate.StartTime - existing.StartTime).TotalDays);
        if (dateDiff > 1) return false;

        var titleScore = Fuzz.Ratio(candidate.Title.ToLower(), existing.Title.ToLower());
        if (titleScore < FuzzyTitleThreshold) return false;

        return true;
    }

    private static bool IsCrossMemberMatch(CalendarEvent candidate, CalendarEvent existing)
    {
        // Must have no member overlap — overlapping members are handled by the per-member loop above.
        var candidateMembers = SplitMembers(candidate.FamilyMemberName);
        var existingMembers = SplitMembers(existing.FamilyMemberName);
        if (candidateMembers.Intersect(existingMembers, StringComparer.OrdinalIgnoreCase).Any())
            return false;

        // Same calendar day (compare dates only)
        if (candidate.StartTime.Date != existing.StartTime.Date)
            return false;

        var titleScore = Fuzz.Ratio(candidate.Title.ToLower(), existing.Title.ToLower());
        if (titleScore < FuzzyTitleThreshold) return false;

        // For timed events, start times must be close enough to be the same occurrence.
        if (candidate.HasTime && existing.HasTime)
        {
            var timeDiff = Math.Abs((candidate.StartTime - existing.StartTime).TotalHours);
            if (timeDiff > CrossMemberTimeThresholdHours) return false;
        }

        return true;
    }

    private static string[] SplitMembers(string memberNames) =>
        memberNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
