using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FamilyCalendar.AI.Extensions;
using FamilyCalendar.AI.Services;
using FamilyCalendar.Api.Validators;
using FamilyCalendar.Calendar.Extensions;
using FamilyCalendar.Calendar.Services;
using FamilyCalendar.Core.Enums;
using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Core.Services;
using FamilyCalendar.Email.Extensions;
using FamilyCalendar.Email.HostedServices;
using FamilyCalendar.Email.Services;
using FamilyCalendar.Infrastructure.Channels;
using FamilyCalendar.Infrastructure.Data;
using FamilyCalendar.Infrastructure.Extensions;
using FamilyCalendar.Infrastructure.HostedServices;
using FamilyCalendar.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums as strings in all JSON responses
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Serilog
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services);

    var otlpEndpoint = ctx.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        cfg.WriteTo.OpenTelemetry(o =>
        {
            o.Endpoint = otlpEndpoint;
            o.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = ctx.HostingEnvironment.ApplicationName
            };
        });
    }
});

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// CORS for local frontend
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
     .AllowAnyHeader()
     .AllowAnyMethod()));

// JWT Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is required");
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// JSON: handle circular references from navigation properties
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

// Infrastructure — skip Npgsql in Testing so WebApplicationFactory can inject in-memory DB
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connStr = builder.Configuration.GetConnectionString("familycalendardb") ?? string.Empty;
    builder.Services.AddInfrastructure(connStr);
}

// Channel
builder.Services.AddSingleton<IEmailProcessingChannel, EmailProcessingChannel>();

// AI
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.Section));
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddAiServices();

// Email
builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection(GmailOptions.Section));
builder.Services.Configure<ReviewNotificationOptions>(builder.Configuration.GetSection(ReviewNotificationOptions.Section));
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddEmailServices();

// Calendar
builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.Section));
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddCalendarServices();

// Domain services
builder.Services.Configure<EventDecisionOptions>(builder.Configuration.GetSection(EventDecisionOptions.Section));
builder.Services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
builder.Services.AddScoped<EventDecisionService>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    // Hosted services that require real external connections
    builder.Services.AddHostedService<GmailPollingService>();
    builder.Services.AddHostedService<EmailProcessingService>();
    builder.Services.AddHostedService<ReviewExpiryService>();
}

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<FamilyCalendar.Api.Validators.ProcessEmailRequestValidator>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Auto-migrate on startup (skip in Testing — in-memory DB needs EnsureCreated instead)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

// ── Auth (public) ──────────────────────────────────────────────────────────────
app.MapPost("/api/auth/login", (LoginRequest req, IConfiguration config) =>
{
    var appPassword = config["APP_PASSWORD"];
    if (string.IsNullOrEmpty(appPassword) || req.Password != appPassword)
        return Results.Unauthorized();

    var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims: [new Claim(ClaimTypes.Name, "family")],
        expires: DateTime.UtcNow.AddDays(90),
        signingCredentials: new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256)
    ));
    return Results.Ok(new { token });
}).AllowAnonymous().WithName("Login");

// ── Emails ────────────────────────────────────────────────────────────────────
app.MapGet("/api/emails", async (IEmailRepository repo) =>
{
    var emails = await repo.GetRecentAsync();
    return Results.Ok(emails.Select(e => new
    {
        e.Id, e.Sender, e.Subject, e.ReceivedAt, e.ProcessedAt,
        Classification = e.Classification.ToString(),
        e.Confidence
    }));
}).WithName("GetEmails").RequireAuthorization();

app.MapGet("/api/emails/{id:guid}", async (Guid id, IEmailRepository repo) =>
{
    var email = await repo.GetByIdAsync(id);
    return email is null ? Results.NotFound() : Results.Ok(email);
}).WithName("GetEmailById").RequireAuthorization();

// ── FamilyMembers ─────────────────────────────────────────────────────────────
app.MapGet("/api/familymembers", async (IFamilyMemberRepository repo) =>
{
    var familyMembers = await repo.GetAllAsync();
    return Results.Ok(familyMembers);
}).WithName("GetFamilyMembers").RequireAuthorization();

app.MapPatch("/api/familymembers/{id:guid}", async (Guid id, UpdateFamilyMemberRequest req, IFamilyMemberRepository repo) =>
{
    var familyMember = await repo.GetByIdAsync(id);
    if (familyMember is null) return Results.NotFound();

    if (req.Description is not null) familyMember.Description = req.Description;

    await repo.UpdateAsync(familyMember);
    return Results.Ok(familyMember);
}).WithName("UpdateFamilyMember").RequireAuthorization();

// ── Events ────────────────────────────────────────────────────────────────────
app.MapGet("/api/events", async (
    string? familyMemberName, DateTimeOffset? from, DateTimeOffset? to,
    IEventRepository repo) =>
{
    var events = await repo.GetAllAsync(familyMemberName, from, to);
    return Results.Ok(events);
}).WithName("GetEvents").RequireAuthorization();

app.MapGet("/api/events/pending-review", async (IEventRepository repo) =>
{
    var events = await repo.GetPendingReviewAsync();
    return Results.Ok(events);
}).WithName("GetPendingReview").RequireAuthorization();

app.MapPost("/api/events/{id:guid}/approve", async (
    Guid id,
    IEventRepository repo,
    IGoogleCalendarService calendarService) =>
{
    var evt = await repo.GetByIdAsync(id);
    if (evt is null) return Results.NotFound();
    if (evt.Status != EventStatus.Pending) return Results.BadRequest("Event is not pending review");

    var calendarEventId = await calendarService.CreateEventAsync(evt);
    evt.CalendarEventId = calendarEventId;
    evt.Status = EventStatus.Created;
    evt.NeedsReview = false;
    await repo.UpdateAsync(evt);

    return Results.Ok(new { evt.Id, evt.Status, calendarEventId });
}).WithName("ApproveEvent").RequireAuthorization();

app.MapPost("/api/events/{id:guid}/reject", async (Guid id, IEventRepository repo) =>
{
    var evt = await repo.GetByIdAsync(id);
    if (evt is null) return Results.NotFound();

    evt.Status = EventStatus.Rejected;
    evt.NeedsReview = false;
    await repo.UpdateAsync(evt);

    return Results.Ok(new { evt.Id, evt.Status });
}).WithName("RejectEvent").RequireAuthorization();

app.MapPatch("/api/events/{id:guid}", async (
    Guid id,
    UpdateEventRequest request,
    IEventRepository repo,
    IValidator<UpdateEventRequest> validator) =>
{
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

    var evt = await repo.GetByIdAsync(id);
    if (evt is null) return Results.NotFound();

    if (request.Title is not null) evt.Title = request.Title;
    if (request.Description is not null) evt.Description = request.Description;
    if (request.StartTime.HasValue) evt.StartTime = request.StartTime.Value;
    if (request.EndTime.HasValue) evt.EndTime = request.EndTime.Value;
    if (request.Location is not null) evt.Location = request.Location;
    if (request.FamilyMemberName is not null) evt.FamilyMemberName = request.FamilyMemberName;

    await repo.UpdateAsync(evt);
    return Results.Ok(evt);
}).WithName("UpdateEvent").RequireAuthorization();

app.MapGet("/api/events/{id:guid}/review-questions", async (Guid id, IEventRepository repo) =>
{
    var evt = await repo.GetByIdAsync(id);
    if (evt is null) return Results.NotFound();
    if (evt.ReviewQuestionsJson is null) return Results.Ok(new List<object>());

    var questions = System.Text.Json.JsonSerializer.Deserialize<List<ReviewQuestion>>(evt.ReviewQuestionsJson) ?? [];
    return Results.Ok(questions);
}).WithName("GetEventReviewQuestions").RequireAuthorization();

app.MapPost("/api/events/{id:guid}/answer-question", async (
    Guid id,
    AnswerQuestionRequest req,
    IFamilyMemberRepository familyMemberRepo,
    IEventRepository eventRepo) =>
{
    var evt = await eventRepo.GetByIdAsync(id);
    if (evt is null) return Results.NotFound();

    var familyMember = await familyMemberRepo.GetByNameAsync(req.FamilyMemberName);
    if (familyMember is null) return Results.NotFound($"FamilyMember '{req.FamilyMemberName}' not found");

    familyMember.Description = string.IsNullOrWhiteSpace(familyMember.Description)
        ? req.NewInfo
        : $"{familyMember.Description} {req.NewInfo}.";

    await familyMemberRepo.UpdateAsync(familyMember);
    return Results.Ok(familyMember);
}).WithName("AnswerReviewQuestion").RequireAuthorization();

// ── Manual email submission (testing / future use) ────────────────────────────
app.MapPost("/api/process-email", async (
    ProcessEmailRequest request,
    IEmailRepository emailRepo,
    IEmailProcessingChannel channel,
    IValidator<ProcessEmailRequest> validator) =>
{
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

    if (await emailRepo.ExistsByMessageIdAsync(request.MessageId))
        return Results.Conflict("Email already processed");

    var email = new Email
    {
        Id = Guid.NewGuid(),
        MessageId = request.MessageId,
        Sender = request.Sender,
        Subject = request.Subject,
        Body = request.Body,
        ReceivedAt = request.ReceivedAt,
        Classification = EmailClassification.Pending
    };

    await emailRepo.AddAsync(email);
    await channel.WriteAsync(email);

    return Results.Accepted($"/api/emails/{email.Id}", new { email.Id, status = "queued" });
}).WithName("ProcessEmail").RequireAuthorization();

// ── Email sync ────────────────────────────────────────────────────────────────
app.MapPost("/api/sync-emails", async (
    GmailPollingService poller,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        await poller.PollAsync(ct);
        return Results.Ok();
    }
    catch (Google.Apis.Auth.OAuth2.Responses.TokenResponseException ex)
    {
        logger.LogError(ex, "Gmail OAuth token rejected during manual sync");
        return Results.Problem(
            title: "Gmail authentication failed",
            detail: $"Refresh token rejected by Google: {ex.Error.Error}. Regenerate it with tools/GetRefreshToken and update .env.",
            statusCode: StatusCodes.Status401Unauthorized);
    }
}).WithName("SyncEmails").RequireAuthorization();

// ── Admin ─────────────────────────────────────────────────────────────────────
// Removes the FamilyCalendarProcessed label from all Gmail messages so they can be re-imported.
// Use this after a fresh database reset to re-process historical emails.
app.MapPost("/api/admin/reset-gmail-labels", async (
    HttpContext context,
    GmailClientService gmailClient,
    IOptions<GmailOptions> gmailOptions) =>
{
    if (!context.Request.Headers.TryGetValue("X-Admin-Key", out var key) ||
        key != context.RequestServices.GetRequiredService<IConfiguration>()["AdminKey"])
    {
        context.Response.StatusCode = 401;
        return;
    }

    var labelId = await gmailClient.EnsureLabelExistsAsync(gmailOptions.Value.ProcessedLabelName);
    var count = await gmailClient.RemoveLabelFromAllAsync(labelId);
    await Results.Ok(new { message = $"Removed label from {count} messages. Next poll will re-import them." }).ExecuteAsync(context);
}).WithName("ResetGmailLabels");

// Pushes all Approved (auto-approved but not yet in calendar) events to Google Calendar.
app.MapPost("/api/admin/sync-approved-events", async (
    HttpContext context,
    IEventRepository eventRepo,
    IGoogleCalendarService calendarService) =>
{
    if (!context.Request.Headers.TryGetValue("X-Admin-Key", out var key) ||
        key != context.RequestServices.GetRequiredService<IConfiguration>()["AdminKey"])
    {
        context.Response.StatusCode = 401;
        return;
    }

    var allEvents = await eventRepo.GetAllAsync(null, null, null);
    var approved = allEvents.Where(e => e.Status == EventStatus.Approved).ToList();

    int synced = 0, failed = 0;
    foreach (var evt in approved)
    {
        try
        {
            var calendarEventId = await calendarService.CreateEventAsync(evt);
            evt.CalendarEventId = calendarEventId;
            evt.Status = EventStatus.Created;
            await eventRepo.UpdateAsync(evt);
            synced++;
        }
        catch
        {
            failed++;
        }
    }
    await Results.Ok(new { synced, failed }).ExecuteAsync(context);
}).WithName("SyncApprovedEvents");

app.Run();

public record LoginRequest(string Password);
public record UpdateEventRequest(string? Title, string? Description, DateTimeOffset? StartTime, DateTimeOffset? EndTime, string? Location, string? FamilyMemberName);
public record UpdateFamilyMemberRequest(string? Description);
public record AnswerQuestionRequest(string FamilyMemberName, string NewInfo);
public record ProcessEmailRequest(string MessageId, string Sender, string Subject, string Body, DateTimeOffset ReceivedAt);

// Required for WebApplicationFactory in integration tests
public partial class Program { }
