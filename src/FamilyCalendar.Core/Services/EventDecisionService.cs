using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyCalendar.Core.Services;

public class EventDecisionOptions
{
    public const string Section = "EventDecision";
    public double AutoApproveThreshold { get; set; } = 0.80;
}

public class EventDecisionService(IEventRepository eventRepo, IDuplicateDetectionService duplicateDetection, IOptions<EventDecisionOptions> options, ILogger<EventDecisionService> logger)
{
    private const double AutoCreateThreshold = 0.80; // fallback; overridden by options

    private double Threshold => options.Value.AutoApproveThreshold;

    public async Task<IReadOnlyList<CalendarEvent>> ProcessAsync(Email email, AiAnalysisResult result, CancellationToken ct = default)
    {
        if (!result.Relevant || result.Events.Count == 0)
        {
            logger.LogInformation("Email {MessageId} not relevant or contains no events", email.MessageId);
            return [];
        }

        var created = new List<CalendarEvent>();
        foreach (var draft in result.Events)
        {
            var evt = await ProcessDraftAsync(email, result.Confidence, draft, ct);
            if (evt is not null) created.Add(evt);
        }
        return created;
    }

    private async Task<CalendarEvent?> ProcessDraftAsync(Email email, double emailConfidence, AiEventDraft draft, CancellationToken ct)
    {
        if (draft.FamilyMembers.Count == 0)
        {
            logger.LogWarning("Email {EmailId}: event '{Title}' has no identifiable family members — skipping",
                email.Id, draft.Title);
            return null;
        }

        var familyMemberName = string.Join(", ", draft.FamilyMembers);

        var sourceNote = $"Källa: {email.Subject} ({email.ReceivedAt:yyyy-MM-dd})";
        var candidate = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            FamilyMemberName = familyMemberName,
            Title = draft.Title ?? draft.EventType ?? "Aktivitet",
            Description = draft.Summary != null ? $"{draft.Summary}\n\n{sourceNote}" : sourceNote,
            StartTime = (draft.Start ?? DateTimeOffset.UtcNow.AddDays(1)).ToUniversalTime(),
            EndTime = draft.End?.ToUniversalTime(),
            HasTime = draft.HasTime,
            Location = draft.Location,
            CalendarProvider = "google",
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (draft.Action == "cancel")
            return await CancelMatchingEventAsync(candidate, ct);

        var existing = await duplicateDetection.FindMatchAsync(candidate, ct);
        if (existing != null)
        {
            if (!HasNewInformation(candidate, existing, draft.Summary))
            {
                logger.LogInformation("Pure duplicate detected for {FamilyMembers}, skipping '{Title}'",
                    familyMemberName, candidate.Title);
                return null;
            }

            MergeInto(existing, candidate, draft.Summary);
            await eventRepo.UpdateAsync(existing, ct);
            logger.LogInformation("Updated existing event '{Title}' for {FamilyMembers} with new information",
                existing.Title, existing.FamilyMemberName);

            // Only return events already in Google Calendar so the caller can push the update.
            // Pending events are silently updated in the DB and will be reviewed with the merged info.
            return existing.CalendarEventId != null ? existing : null;
        }

        bool needsReview = ShouldRequireReview(emailConfidence, draft, Threshold);
        candidate.NeedsReview = needsReview;
        candidate.Status = needsReview ? EventStatus.Pending : EventStatus.Approved;

        await eventRepo.AddAsync(candidate, ct);
        logger.LogInformation("Created event '{Title}' for {FamilyMembers}, needsReview={NeedsReview}",
            candidate.Title, familyMemberName, needsReview);
        return candidate;
    }

    private async Task<CalendarEvent?> CancelMatchingEventAsync(CalendarEvent candidate, CancellationToken ct)
    {
        var existing = await duplicateDetection.FindMatchAsync(candidate, ct);
        if (existing == null)
        {
            logger.LogInformation("Cancel requested for '{Title}' but no matching event found — ignoring",
                candidate.Title);
            return null;
        }

        existing.Status = EventStatus.Rejected;
        existing.NeedsReview = false;
        await eventRepo.UpdateAsync(existing, ct);
        logger.LogInformation("Cancelled event '{Title}' for {FamilyMembers}", existing.Title, existing.FamilyMemberName);

        // Return the event so EmailProcessingService can delete it from Google Calendar
        // if it was already created there.
        return existing.CalendarEventId != null ? existing : null;
    }

    private static bool ShouldRequireReview(double emailConfidence, AiEventDraft draft, double threshold) =>
        emailConfidence < threshold ||
        draft.RequiresManualReview ||
        draft.IsRecurring ||
        draft.Start == null ||
        !draft.HasTime;

    private static bool HasNewInformation(CalendarEvent candidate, CalendarEvent existing, string? aiSummary = null)
    {
        // New family members not yet on the existing event
        var existingMembers = SplitMembers(existing.FamilyMemberName);
        var candidateMembers = SplitMembers(candidate.FamilyMemberName);
        if (candidateMembers.Except(existingMembers, StringComparer.OrdinalIgnoreCase).Any())
            return true;

        // Location added where none existed
        if (!string.IsNullOrWhiteSpace(candidate.Location) && string.IsNullOrWhiteSpace(existing.Location))
            return true;

        // End time added where none existed
        if (candidate.EndTime.HasValue && !existing.EndTime.HasValue)
            return true;

        // Compare only the AI-extracted summary (not the appended source note) against
        // existing description to avoid treating every repeat email as "new information".
        var compareDesc = aiSummary;
        if (!string.IsNullOrWhiteSpace(compareDesc))
        {
            if (string.IsNullOrWhiteSpace(existing.Description))
                return true;
            if (!existing.Description.Contains(compareDesc, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void MergeInto(CalendarEvent existing, CalendarEvent candidate, string? aiSummary)
    {
        // Union of family members, preserving original order
        var existingMembers = SplitMembers(existing.FamilyMemberName);
        var newMembers = SplitMembers(candidate.FamilyMemberName)
            .Where(m => !existingMembers.Contains(m, StringComparer.OrdinalIgnoreCase));
        existing.FamilyMemberName = string.Join(", ", existingMembers.Concat(newMembers));

        if (string.IsNullOrWhiteSpace(existing.Location) && !string.IsNullOrWhiteSpace(candidate.Location))
            existing.Location = candidate.Location;

        if (!existing.EndTime.HasValue && candidate.EndTime.HasValue)
            existing.EndTime = candidate.EndTime;

        // Append only the new AI-extracted summary (not the source note) to keep descriptions readable.
        if (!string.IsNullOrWhiteSpace(aiSummary))
        {
            if (string.IsNullOrWhiteSpace(existing.Description))
                existing.Description = aiSummary;
            else if (!existing.Description.Contains(aiSummary, StringComparison.OrdinalIgnoreCase))
                existing.Description = $"{existing.Description}\n\n{aiSummary}";
        }
    }

    private static string[] SplitMembers(string memberNames) =>
        memberNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
