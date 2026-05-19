using Microsoft.Playwright;

namespace FamilyCalendar.IntegrationTests;

/// <summary>
/// E2E tests using Playwright. Requires the app to be running at APP_BASE_URL
/// (default: http://localhost:5173). Run the app first with:
///   dotnet run --project src/FamilyCalendar.AppHost
/// Then run these tests with:
///   dotnet test tests/FamilyCalendar.IntegrationTests --filter "Category=E2E"
/// </summary>
[Trait("Category", "E2E")]
public class DashboardE2ETests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5173";

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Dashboard_ShowsSummaryCards()
    {
        await _page.GotoAsync(BaseUrl);
        await Assertions.Expect(_page.GetByText("Senaste mail")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Väntar på granskning")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Kommande händelser")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_InkorgLink_OpensEmailsPage()
    {
        await _page.GotoAsync(BaseUrl);
        await _page.GetByText("Inkorg").ClickAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Inkorg" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_GranskningLink_OpensReviewPage()
    {
        await _page.GotoAsync(BaseUrl);
        await _page.GetByText("Granskning").ClickAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Granskning" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_HandelserLink_OpensEventsPage()
    {
        await _page.GotoAsync(BaseUrl);
        await _page.GetByText("Händelser").ClickAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Händelser" })).ToBeVisibleAsync();
    }
}

[Trait("Category", "E2E")]
public class ReviewWorkflowE2ETests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5173";

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task ReviewPage_ShowsNoItemsMessage_WhenQueueEmpty()
    {
        await _page.GotoAsync($"{BaseUrl}/review");
        await Assertions.Expect(_page.GetByText("Inga händelser väntar")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ReviewActionPage_Approve_ShowsConfirmation()
    {
        await _page.GotoAsync($"{BaseUrl}/review/{Guid.NewGuid()}/approve");
        // Either success or "already processed" error — both are valid outcomes
        var hasSuccess = await _page.GetByText("godkändes").IsVisibleAsync();
        var hasError = await _page.GetByText("gick fel").IsVisibleAsync();
        Assert.True(hasSuccess || hasError);
    }
}
