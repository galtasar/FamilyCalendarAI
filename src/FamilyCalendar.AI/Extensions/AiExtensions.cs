using FamilyCalendar.AI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyCalendar.AI.Extensions;

public static class AiExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services.AddSingleton<EmailAnalyzer>();
        services.AddSingleton<FamilyMemberProfileAnalyzer>();
        return services;
    }
}
