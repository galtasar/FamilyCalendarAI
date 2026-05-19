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

        var expired = await eventRepo.GetExpiredPendingAsync(DateTimeOffset.UtcNow, ct);
        foreach (var evt in expired)
        {
            evt.Status = EventStatus.Rejected;
            await eventRepo.UpdateAsync(evt, ct);
            logger.LogInformation("Auto-rejected expired event {EventId}: {Title}", evt.Id, evt.Title);
        }
    }
}
