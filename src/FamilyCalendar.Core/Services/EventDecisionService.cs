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

public class EventDecisionService(
    IEventRepository eventRepo,
    IDuplicateDetectionService duplicateDetection,
    IDescriptionEvaluationService descriptionEvaluator,
    IOptions<EventDecisionOptions> options,
    ILogger<EventDecisionService> logger)
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
            bool hasStructuralNewInfo = HasNewInformation(candidate, existing);

            // Use AI to evaluate if the new summary adds information not yet in the description.
            string? mergedDescription = null;
            if (!string.IsNullOrWhiteSpace(draft.Summary))
                mergedDescription = await descriptionEvaluator.MergeAsync(existing.Description, draft.Summary, ct);

            if (!hasStructuralNewInfo && mergedDescription == null)
            {
                logger.LogInformation("Pure duplicate detected for {FamilyMembers}, skipping '{Title}'",
                    familyMemberName, candidate.Title);
                return null;
            }

            bool crossMemberMerge = MergeInto(existing, candidate, mergedDescription, sourceNote);
            if (crossMemberMerge)
                existing.NeedsReview = true;

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

    private static bool HasNewInformation(CalendarEvent candidate, CalendarEvent existing)
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

        // Time precision upgraded: follow-up email provides a specific time where none existed
        if (candidate.HasTime && !existing.HasTime)
            return true;

        return false;
    }

    // Returns true if new family members were added (indicating a cross-member merge).
    private static bool MergeInto(CalendarEvent existing, CalendarEvent candidate, string? mergedDescription, string? sourceNote)
    {
        // Union of family members, preserving original order
        var existingMembers = SplitMembers(existing.FamilyMemberName);
        var newMembers = SplitMembers(candidate.FamilyMemberName)
            .Where(m => !existingMembers.Contains(m, StringComparer.OrdinalIgnoreCase))
            .ToList();
        bool newMembersAdded = newMembers.Count > 0;
        if (newMembersAdded)
            existing.FamilyMemberName = string.Join(", ", existingMembers.Concat(newMembers));

        if (string.IsNullOrWhiteSpace(existing.Location) && !string.IsNullOrWhiteSpace(candidate.Location))
            existing.Location = candidate.Location;

        if (!existing.EndTime.HasValue && candidate.EndTime.HasValue)
            existing.EndTime = candidate.EndTime;

        // Upgrade time precision when the new email provides a specific time where none existed.
        if (candidate.HasTime && !existing.HasTime)
        {
            existing.StartTime = candidate.StartTime;
            existing.HasTime = true;
            if (candidate.EndTime.HasValue)
                existing.EndTime = candidate.EndTime;
        }

        // Apply AI-merged description and append the new source note for traceability.
        if (mergedDescription != null)
        {
            var description = mergedDescription;
            if (!string.IsNullOrWhiteSpace(sourceNote) && !description.Contains(sourceNote))
                description = $"{description}\n\n{sourceNote}";
            existing.Description = description;
        }

        return newMembersAdded;
    }

    private static string[] SplitMembers(string memberNames) =>
        memberNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
