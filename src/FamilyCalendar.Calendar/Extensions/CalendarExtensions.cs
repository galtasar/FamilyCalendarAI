using FamilyCalendar.Calendar.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyCalendar.Calendar.Extensions;

public static class CalendarExtensions
{
    public static IServiceCollection AddCalendarServices(this IServiceCollection services)
    {
        services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
        return services;
    }
}
