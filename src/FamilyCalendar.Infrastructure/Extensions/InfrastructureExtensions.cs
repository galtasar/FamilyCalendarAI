using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Infrastructure.Data;
using FamilyCalendar.Infrastructure.Repositories;
using FamilyCalendar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyCalendar.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IFamilyMemberRepository, FamilyMemberRepository>();

        return services;
    }
}
