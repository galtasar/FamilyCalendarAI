using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyCalendar.AI.Services;
using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using EmailModel = FamilyCalendar.Core.Models.Email;

namespace FamilyCalendar.IntegrationTests;

public class EventsApiTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(EmailModel email, CalendarEvent evt)> SeedPendingEventAsync(string? reviewQuestionsJson = null)
    {
        using var scope = factory.Services.CreateScope();
        var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();

        var email = await emailRepo.AddAsync(new EmailModel
        {
            Id = Guid.NewGuid(),
            MessageId = $"msg-{Guid.NewGuid()}",
            Sender = "skola@vattholmaskolan.se",
            Subject = "Föräldramöte",
            Body = "...",
            ReceivedAt = DateTimeOffset.UtcNow,
            Classification = EmailClassification.Relevant
        });

        var evt = await eventRepo.AddAsync(new CalendarEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            FamilyMemberName = "Vera",
            Title = "Föräldramöte klass 5",
            StartTime = DateTimeOffset.UtcNow.AddDays(7),
            ReviewQuestionsJson = reviewQuestionsJson,
            Status = EventStatus.Pending,
            NeedsReview = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return (email, evt);
    }

    [Fact]
    public async Task GetFamilyMembers_ReturnsSeededFamilyMembers()
    {
        var response = await _client.GetAsync("/api/familymembers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Vera", body);
        Assert.Contains("Tage", body);
        Assert.Contains("Sixten", body);
        Assert.Contains("Folke", body);
    }

    [Fact]
    public async Task GetPendingReview_Empty_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/events/pending-review");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingReview_WithPendingEvent_ReturnsIt()
    {
        var (_, evt) = await SeedPendingEventAsync();

        var response = await _client.GetAsync("/api/events/pending-review");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(evt.Title, body);
    }

    [Fact]
    public async Task ApproveEvent_ValidPending_Returns200AndCreatesCalendarEvent()
    {
        var (_, evt) = await SeedPendingEventAsync();

        factory.CalendarServiceMock
            .Setup(s => s.CreateEventAsync(It.IsAny<CalendarEvent>()))
            .ReturnsAsync("google-event-id-123");

        var response = await _client.PostAsync($"/api/events/{evt.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("google-event-id-123", body);
    }

    [Fact]
    public async Task ApproveEvent_NotFound_Returns404()
    {
        var response = await _client.PostAsync($"/api/events/{Guid.NewGuid()}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RejectEvent_ValidPending_Returns200()
    {
        var (_, evt) = await SeedPendingEventAsync();

        var response = await _client.PostAsync($"/api/events/{evt.Id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchEvent_UpdatesTitle_Returns200()
    {
        var (_, evt) = await SeedPendingEventAsync();

        var response = await _client.PatchAsJsonAsync($"/api/events/{evt.Id}", new { title = "Uppdaterat möte" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Uppdaterat möte", body);
    }

    [Fact]
    public async Task PatchFamilyMember_UpdatesActivities_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var familyMemberRepo = scope.ServiceProvider.GetRequiredService<IFamilyMemberRepository>();
        var familyMember = await familyMemberRepo.GetByNameAsync("Vera");
        Assert.NotNull(familyMember);

        var response = await _client.PatchAsJsonAsync($"/api/familymembers/{familyMember!.Id}", new { activities = "Spelar piano", classGroup = "Klass 5B" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Spelar piano", body);
        Assert.Contains("Klass 5B", body);
    }

    [Fact]
    public async Task GetReviewQuestions_ReturnsStoredQuestions()
    {
        var questionsJson = JsonSerializer.Serialize(new List<ReviewQuestion>
        {
            new() { Question = "Vilket barn rider?", Context = "Mailet är från ridskolan" }
        });

        var (_, evt) = await SeedPendingEventAsync(questionsJson);

        var response = await _client.GetAsync($"/api/events/{evt.Id}/review-questions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Vilket barn rider?", body);
    }

    [Fact]
    public async Task AnswerQuestion_UpdatesFamilyMemberActivities_Returns200()
    {
        var (_, evt) = await SeedPendingEventAsync();

        var response = await _client.PostAsJsonAsync($"/api/events/{evt.Id}/answer-question", new
        {
            familyMemberName = "Tage",
            newInfo = "Rider på Frostgårdens ridskola"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var familyMemberRepo = scope.ServiceProvider.GetRequiredService<IFamilyMemberRepository>();
        var familyMember = await familyMemberRepo.GetByNameAsync("Tage");
        Assert.Contains("Rider på Frostgårdens ridskola", familyMember?.Description);
    }

    [Fact]
    public async Task ProcessEmail_NewEmail_ReturnsAccepted()
    {
        var payload = new
        {
            messageId = $"new-msg-{Guid.NewGuid()}",
            sender = "test@skola.se",
            subject = "Utflykt fredag",
            body = "Sixten ska ha utflykt nästa fredag.",
            receivedAt = DateTimeOffset.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/process-email", payload);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ProcessEmail_DuplicateMessageId_ReturnsConflict()
    {
        var messageId = $"dup-{Guid.NewGuid()}";

        // First submission
        var payload = new
        {
            messageId,
            sender = "test@skola.se",
            subject = "Test",
            body = "Test",
            receivedAt = DateTimeOffset.UtcNow
        };
        await _client.PostAsJsonAsync("/api/process-email", payload);

        // Second submission with same messageId
        var response = await _client.PostAsJsonAsync("/api/process-email", payload);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
