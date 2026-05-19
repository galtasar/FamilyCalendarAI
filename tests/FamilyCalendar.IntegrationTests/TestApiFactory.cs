using FamilyCalendar.Calendar.Services;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Infrastructure.Channels;
using FamilyCalendar.Infrastructure.Data;
using FamilyCalendar.Infrastructure.Repositories;
using FamilyCalendar.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FamilyCalendar.IntegrationTests;

/// <summary>
/// WebApplicationFactory that uses in-memory database and stubs external services.
/// Program.cs skips Npgsql registration in the "Testing" environment, so this factory
/// registers InMemory EF Core cleanly with no provider conflict.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    public Mock<IGoogleCalendarService> CalendarServiceMock { get; } = new(MockBehavior.Loose);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Compute a stable db name once so all scopes share the same in-memory store
            var dbName = $"TestDb_{Guid.NewGuid()}";

            // Register infrastructure with in-memory DB (Npgsql was skipped by Program.cs)
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            services.AddScoped<IEmailRepository, EmailRepository>();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IFamilyMemberRepository, FamilyMemberRepository>();
            services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();

            // Replace channel with fresh singleton
            services.AddSingleton<IEmailProcessingChannel, EmailProcessingChannel>();

            // Calendar service mock
            services.AddSingleton(CalendarServiceMock.Object);
        });
    }
}
