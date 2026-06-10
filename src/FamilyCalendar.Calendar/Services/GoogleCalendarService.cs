using FamilyCalendar.Core.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyCalendar.Calendar.Services;

public class GoogleCalendarOptions
{
    public const string Section = "GoogleCalendar";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = "me";
    public string CalendarName { get; set; } = "Familjekalender";
    public string? CalendarId { get; set; }
}

public class GoogleCalendarService(IOptions<GoogleCalendarOptions> options, ILogger<GoogleCalendarService> logger) : IGoogleCalendarService
{
    private CalendarService? _client;
    private string? _calendarId;

    public async Task<CalendarService> GetClientAsync()
    {
        if (_client != null) return _client;

        var opt = options.Value;
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = opt.ClientId, ClientSecret = opt.ClientSecret },
            Scopes = [CalendarService.Scope.Calendar]
        });

        var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse { RefreshToken = opt.RefreshToken };
        var credential = new UserCredential(flow, opt.UserId, token);

        _client = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FamilyCalendarAI"
        });

        return _client;
    }

    public async Task<string> GetOrCreateFamiljekalenderAsync()
    {
        if (_calendarId != null) return _calendarId;

        var opt = options.Value;
        if (!string.IsNullOrEmpty(opt.CalendarId))
        {
            _calendarId = opt.CalendarId;
            return _calendarId;
        }

        var client = await GetClientAsync();
        var list = await client.CalendarList.List().ExecuteAsync();
        var existing = list.Items?.FirstOrDefault(c => c.Summary == opt.CalendarName);

        if (existing != null)
        {
            _calendarId = existing.Id;
            logger.LogInformation("Found existing calendar: {CalendarId}", _calendarId);
            return _calendarId;
        }

        var newCal = await client.Calendars.Insert(new Google.Apis.Calendar.v3.Data.Calendar
        {
            Summary = opt.CalendarName,
            TimeZone = "Europe/Stockholm"
        }).ExecuteAsync();

        _calendarId = newCal.Id;
        logger.LogInformation("Created new calendar '{Name}': {CalendarId}", opt.CalendarName, _calendarId);
        return _calendarId;
    }

    public async Task<string> CreateEventAsync(CalendarEvent calendarEvent)
    {
        var client = await GetClientAsync();
        var calendarId = await GetOrCreateFamiljekalenderAsync();

        var googleEvent = MapToGoogleEvent(calendarEvent);
        var created = await client.Events.Insert(googleEvent, calendarId).ExecuteAsync();

        logger.LogInformation("Created Google Calendar event {EventId} for {FamilyMember}", created.Id, calendarEvent.FamilyMemberName);
        return created.Id;
    }

    public async Task UpdateEventAsync(CalendarEvent calendarEvent)
    {
        if (string.IsNullOrEmpty(calendarEvent.CalendarEventId))
        {
            logger.LogWarning("UpdateEventAsync called for event {EventId} '{Title}' with no CalendarEventId — skipping sync", calendarEvent.Id, calendarEvent.Title);
            return;
        }

        var client = await GetClientAsync();
        var calendarId = await GetOrCreateFamiljekalenderAsync();

        var googleEvent = MapToGoogleEvent(calendarEvent);
        await client.Events.Update(googleEvent, calendarId, calendarEvent.CalendarEventId).ExecuteAsync();
    }

    public async Task DeleteEventAsync(CalendarEvent calendarEvent)
    {
        if (string.IsNullOrEmpty(calendarEvent.CalendarEventId))
        {
            logger.LogWarning("DeleteEventAsync called for event {EventId} '{Title}' with no CalendarEventId — skipping sync", calendarEvent.Id, calendarEvent.Title);
            return;
        }

        var client = await GetClientAsync();
        var calendarId = await GetOrCreateFamiljekalenderAsync();

        await client.Events.Delete(calendarId, calendarEvent.CalendarEventId).ExecuteAsync();
    }

    private static readonly TimeZoneInfo StockholmTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private static Event MapToGoogleEvent(CalendarEvent evt)
    {
        var googleEvent = new Event
        {
            Summary = $"{evt.FamilyMemberName}: {evt.Title}",
            Description = evt.Description,
            Location = evt.Location,
        };

        if (!evt.HasTime)
        {
            // No specific time — create as all-day event.
            // StartTime is stored as UTC, so convert to Stockholm local date first;
            // otherwise a Stockholm midnight (00:00+02:00 = 22:00 UTC previous day)
            // would produce the wrong date string.
            var localDate = TimeZoneInfo.ConvertTime(evt.StartTime, StockholmTz);
            googleEvent.Start = new EventDateTime { Date = localDate.ToString("yyyy-MM-dd") };
            googleEvent.End = new EventDateTime { Date = localDate.AddDays(1).ToString("yyyy-MM-dd") };
        }
        else
        {
            googleEvent.Start = new EventDateTime { DateTimeDateTimeOffset = evt.StartTime, TimeZone = "Europe/Stockholm" };
            googleEvent.End = new EventDateTime { DateTimeDateTimeOffset = evt.EndTime ?? evt.StartTime.AddHours(1), TimeZone = "Europe/Stockholm" };
        }

        return googleEvent;
    }
}
