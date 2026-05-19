using System.Net;
using System.Net.Http.Json;
using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using EmailModel = FamilyCalendar.Core.Models.Email;

namespace FamilyCalendar.IntegrationTests;

public class EmailsApiTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetEmails_Empty_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/emails");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("[]", body);
    }

    [Fact]
    public async Task GetEmailById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/emails/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEmailById_Existing_Returns200()
    {
        // Seed an email directly
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
        var email = await repo.AddAsync(new EmailModel
        {
            Id = Guid.NewGuid(),
            MessageId = "test-msg-api",
            Sender = "test@skola.se",
            Subject = "Test",
            Body = "Testbrev",
            ReceivedAt = DateTimeOffset.UtcNow,
            Classification = EmailClassification.Relevant
        });

        var response = await _client.GetAsync($"/api/emails/{email.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
