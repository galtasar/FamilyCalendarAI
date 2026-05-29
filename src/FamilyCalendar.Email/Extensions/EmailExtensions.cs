using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Email.HostedServices;
using FamilyCalendar.Email.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyCalendar.Email.Extensions;

public static class EmailExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        services.AddSingleton<GmailClientService>();
        services.AddSingleton<EmailParser>();
        services.AddScoped<IReviewNotificationService, ReviewNotificationService>();
        services.AddSingleton<GmailPollingService>();
        services.AddHostedService(sp => sp.GetRequiredService<GmailPollingService>());
        services.AddHostedService<WeeklySummaryService>();
        return services;
    }
}
