using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using Microsoft.Extensions.Logging;

namespace FamilyCalendar.Core.Services;

public class EventDecisionService(IEventRepository eventRepo, IDuplicateDetectionService duplicateDetection, ILogger<EventDecisionService> logger)
{
    private const double AutoCreateThreshold = 0.80;

    public async Task<IReadOnlyList<CalendarEvent>> ProcessAsync(Email email, AiAnalysisResult result, CancellationToken ct = default)
    {
        if (!result.Relevant || !result.RequiresCalendarEvent)
        {
            logger.LogInformation("Email {MessageId} is not relevant or does not require a calendar event", email.MessageId);
            return [];
        }

        if (result.FamilyMembers.Count == 0)
        {
            logger.LogWarning("Email {EmailId} produced no identifiable family members — skipping event creation", email.Id);
            return [];
        }

        var familyMemberName = string.Join(", ", result.FamilyMembers);

        var evt = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            FamilyMemberName = familyMemberName,
            Title = result.Title ?? result.EventType ?? "Aktivitet",
            Description = result.Summary,
            StartTime = (result.Start ?? DateTimeOffset.UtcNow.AddDays(1)).ToUniversalTime(),
            EndTime = result.End?.ToUniversalTime(),
            Location = result.Location,
            CalendarProvider = "google",
            CreatedAt = DateTimeOffset.UtcNow
        };

        bool isDuplicate = await duplicateDetection.IsDuplicateAsync(evt, ct);
        if (isDuplicate)
        {
            logger.LogInformation("Duplicate event detected for {FamilyMembers}, skipping", familyMemberName);
            return [];
        }

        bool needsReview = ShouldRequireReview(result);
        evt.NeedsReview = needsReview;
        evt.Status = needsReview ? EventStatus.Pending : EventStatus.Approved;

        await eventRepo.AddAsync(evt, ct);
        logger.LogInformation("Created event {Title} for {FamilyMembers}, needsReview={NeedsReview}", evt.Title, familyMemberName, needsReview);

        return [evt];
    }

    private static bool ShouldRequireReview(AiAnalysisResult result) =>
        result.Confidence < AutoCreateThreshold ||
        result.RequiresManualReview ||
        result.IsRecurring ||
        result.Start == null ||
        !result.HasTime;
}
