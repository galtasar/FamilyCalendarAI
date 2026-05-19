using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Email.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyCalendar.Email.Services;

public class ReviewNotificationOptions
{
    public const string Section = "ReviewNotification";
    public string AppBaseUrl { get; set; } = "http://localhost:5000";
    public string RecipientEmail { get; set; } = "dahl.aicalendar@gmail.com";
}

public class ReviewNotificationService(
    GmailClientService gmailClient,
    IOptions<ReviewNotificationOptions> options,
    ILogger<ReviewNotificationService> logger) : IReviewNotificationService
{
    public async Task SendReviewNotificationAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        var opt = options.Value;
        var approveUrl = $"{opt.AppBaseUrl}/review/{calendarEvent.Id}/approve";
        var rejectUrl = $"{opt.AppBaseUrl}/review/{calendarEvent.Id}/reject";
        var reviewUrl = $"{opt.AppBaseUrl}/review/{calendarEvent.Id}";

        var subject = $"Granskning behövs: {calendarEvent.Title}";
        var startStr = calendarEvent.EndTime == null
            ? calendarEvent.StartTime.ToString("yyyy-MM-dd") + " (heldag)"
            : calendarEvent.StartTime.ToString("yyyy-MM-dd HH:mm");

        var textBody = $"""
            Ny kalenderaktivitet behöver granskas.

            Familjemedlem: {calendarEvent.FamilyMemberName}
            Titel: {calendarEvent.Title}
            Tid: {startStr}
            Plats: {calendarEvent.Location ?? "–"}
            Beskrivning: {calendarEvent.Description ?? "–"}

            Godkänn: {approveUrl}
            Avvisa: {rejectUrl}
            Granska i appen: {reviewUrl}
            """;

        var htmlBody = $"""
            <html><body>
            <h2>Ny kalenderaktivitet behöver granskas</h2>
            <table>
              <tr><td><b>Familjemedlem:</b></td><td>{calendarEvent.FamilyMemberName}</td></tr>
              <tr><td><b>Titel:</b></td><td>{calendarEvent.Title}</td></tr>
              <tr><td><b>Tid:</b></td><td>{startStr}</td></tr>
              <tr><td><b>Plats:</b></td><td>{calendarEvent.Location ?? "–"}</td></tr>
              <tr><td><b>Beskrivning:</b></td><td>{calendarEvent.Description ?? "–"}</td></tr>
            </table>
            <p>
              <a href="{approveUrl}" style="background:#4caf50;color:white;padding:8px 16px;text-decoration:none;border-radius:4px;margin-right:8px">✅ Godkänn</a>
              <a href="{rejectUrl}" style="background:#f44336;color:white;padding:8px 16px;text-decoration:none;border-radius:4px;margin-right:8px">❌ Avvisa</a>
              <a href="{reviewUrl}" style="background:#2196f3;color:white;padding:8px 16px;text-decoration:none;border-radius:4px">🔍 Granska i appen</a>
            </p>
            </body></html>
            """;

        try
        {
            await gmailClient.SendEmailAsync(opt.RecipientEmail, subject, htmlBody, textBody);
            logger.LogInformation("Review notification sent for event {EventId}", calendarEvent.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send review notification for event {EventId}", calendarEvent.Id);
        }
    }
}
