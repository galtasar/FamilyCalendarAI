using FamilyCalendar.AI.Services;
using FamilyCalendar.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyCalendar.AI.Extensions;

public static class AiExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services.AddSingleton<EmailAnalyzer>();
        services.AddSingleton<FamilyMemberProfileAnalyzer>();
        services.AddScoped<IDescriptionEvaluationService, DescriptionEvaluationService>();
        return services;
    }
}
