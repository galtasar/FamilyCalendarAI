using FamilyCalendar.AI.Services;
using FamilyCalendar.Calendar.Services;
using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FamilyCalendar.Infrastructure.HostedServices;

public class EmailProcessingService(
    IEmailProcessingChannel channel,
    IServiceScopeFactory scopeFactory,
    EmailAnalyzer analyzer,
    FamilyMemberProfileAnalyzer familyMemberProfileAnalyzer,
    ILogger<EmailProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email processing service started");

        using (var scope = scopeFactory.CreateScope())
        {
            var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
            var pending = await emailRepo.GetPendingAsync(stoppingToken);
            if (pending.Count > 0)
            {
                logger.LogInformation("Re-queuing {Count} pending emails from database", pending.Count);
                foreach (var email in pending)
                    await channel.WriteAsync(email, stoppingToken);
            }
        }

        await foreach (var email in channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessEmailAsync(email, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process email {MessageId}", email.MessageId);
            }
        }
    }

    private async Task ProcessEmailAsync(Email email, CancellationToken ct)
    {
        logger.LogInformation("Processing email {MessageId}: {Subject}", email.MessageId, email.Subject);

        using var scope = scopeFactory.CreateScope();
        var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
        var familyMemberRepo = scope.ServiceProvider.GetRequiredService<IFamilyMemberRepository>();
        var decisionService = scope.ServiceProvider.GetRequiredService<EventDecisionService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IReviewNotificationService>();
        var calendarService = scope.ServiceProvider.GetRequiredService<IGoogleCalendarService>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();

        var familyMembers = await familyMemberRepo.GetAllAsync(ct);
        var result = await analyzer.AnalyzeAsync(email, familyMembers, ct);

        email.ProcessedAt = DateTimeOffset.UtcNow;

        if (result == null)
        {
            email.Classification = EmailClassification.Error;
            await emailRepo.UpdateAsync(email, ct);
            return;
        }

        email.Classification = result.Relevant ? EmailClassification.Relevant : EmailClassification.Irrelevant;
        email.Confidence = result.Confidence;
        email.RawAiResponse = JsonSerializer.Serialize(result);
        await emailRepo.UpdateAsync(email, ct);

        if (!result.Relevant)
        {
            logger.LogInformation("Email {MessageId} classified as irrelevant", email.MessageId);
            return;
        }

        var events = await decisionService.ProcessAsync(email, result, ct);
        var profileAnalysis = await familyMemberProfileAnalyzer.AnalyzeAsync(email.Subject, email.Body, familyMembers, ct);

        if (profileAnalysis != null)
        {
            await ApplyFamilyMemberProfileUpdatesAsync(profileAnalysis.ProfileUpdates, familyMembers, familyMemberRepo, ct);

            if (profileAnalysis.Questions.Count > 0)
            {
                var questionsJson = JsonSerializer.Serialize(profileAnalysis.Questions);
                foreach (var evt in events.Where(e => e.NeedsReview))
                {
                    evt.ReviewQuestionsJson = questionsJson;
                    await eventRepo.UpdateAsync(evt, ct);
                }
            }
        }

        foreach (var evt in events.Where(e => e.NeedsReview))
        {
            await notificationService.SendReviewNotificationAsync(evt, ct);
        }

        foreach (var evt in events.Where(e => !e.NeedsReview))
        {
            try
            {
                var calendarEventId = await calendarService.CreateEventAsync(evt);
                evt.CalendarEventId = calendarEventId;
                evt.Status = EventStatus.Created;
                await eventRepo.UpdateAsync(evt, ct);
                logger.LogInformation("Auto-created calendar event for {Title}", evt.Title);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create calendar event for {Title}", evt.Title);
                evt.Status = EventStatus.Failed;
                await eventRepo.UpdateAsync(evt, ct);
            }
        }
    }

    private static async Task ApplyFamilyMemberProfileUpdatesAsync(
        IReadOnlyList<FamilyMemberProfileUpdate> updates,
        IReadOnlyList<FamilyMember> familyMembers,
        IFamilyMemberRepository familyMemberRepo,
        CancellationToken ct)
    {
        var familyMemberLookup = familyMembers.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var update in updates)
        {
            if (string.IsNullOrWhiteSpace(update.FamilyMemberName) || string.IsNullOrWhiteSpace(update.NewInfo))
                continue;

            if (!familyMemberLookup.TryGetValue(update.FamilyMemberName, out var familyMember))
                continue;

            familyMember.Description = AppendDescription(familyMember.Description, update.NewInfo);
            await familyMemberRepo.UpdateAsync(familyMember, ct);
        }
    }

    private static string AppendDescription(string? current, string newInfo)
    {
        const int MaxLength = 1000;
        var combined = string.IsNullOrWhiteSpace(current)
            ? newInfo
            : $"{current} {newInfo}.";
        return combined.Length <= MaxLength ? combined : combined[^MaxLength..];
    }
}
