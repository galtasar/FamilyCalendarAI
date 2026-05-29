using FamilyCalendar.Calendar.Services;
using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FamilyCalendar.Infrastructure.HostedServices;

public class ReviewExpiryService(IServiceScopeFactory scopeFactory, ILogger<ReviewExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ExpireOldReviewsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ExpireOldReviewsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var calendarService = scope.ServiceProvider.GetRequiredService<IGoogleCalendarService>();

        var expired = await eventRepo.GetExpiredPendingAsync(DateTimeOffset.UtcNow, ct);
        foreach (var evt in expired)
        {
            if (evt.HasTime)
            {
                try
                {
                    var calendarEventId = await calendarService.CreateEventAsync(evt);
                    evt.CalendarEventId = calendarEventId;
                    evt.Status = EventStatus.Created;
                    evt.NeedsReview = false;
                    evt.SyncError = null;
                    await eventRepo.UpdateAsync(evt, ct);
                    logger.LogInformation("Auto-created expired event {EventId}: '{Title}'", evt.Id, evt.Title);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to auto-create expired event {EventId}: '{Title}'", evt.Id, evt.Title);
                    evt.Status = EventStatus.Failed;
                    evt.SyncError = ex.Message;
                    await eventRepo.UpdateAsync(evt, ct);
                }
            }
            else
            {
                evt.Status = EventStatus.Rejected;
                await eventRepo.UpdateAsync(evt, ct);
                logger.LogInformation("Auto-rejected time-unknown expired event {EventId}: '{Title}'", evt.Id, evt.Title);
            }
        }
    }
}
