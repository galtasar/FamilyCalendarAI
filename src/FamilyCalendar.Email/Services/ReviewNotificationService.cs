using System.Text;
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
    public async Task SendReviewNotificationAsync(IReadOnlyList<CalendarEvent> calendarEvents, CancellationToken ct = default)
    {
        if (calendarEvents.Count == 0)
            return;

        var opt = options.Value;
        var queueUrl = $"{opt.AppBaseUrl}/review";

        var subject = calendarEvents.Count == 1
            ? $"Granskning behövs: {calendarEvents[0].Title}"
            : $"Granskning behövs: {calendarEvents.Count} aktiviteter";

        var heading = calendarEvents.Count == 1
            ? "Ny kalenderaktivitet behöver granskas"
            : $"{calendarEvents.Count} nya kalenderaktiviteter behöver granskas";

        var text = new StringBuilder();
        text.AppendLine(heading + ".");
        text.AppendLine();

        var html = new StringBuilder();
        html.AppendLine("<html><body>");
        html.AppendLine($"<h2>{heading}</h2>");

        for (var i = 0; i < calendarEvents.Count; i++)
        {
            var calendarEvent = calendarEvents[i];
            var approveUrl = $"{opt.AppBaseUrl}/review/{calendarEvent.Id}/approve";
            var rejectUrl = $"{opt.AppBaseUrl}/review/{calendarEvent.Id}/reject";
            var reviewUrl = $"{opt.AppBaseUrl}/review/{calendarEvent.Id}";

            var startStr = calendarEvent.EndTime == null
                ? calendarEvent.StartTime.ToString("yyyy-MM-dd") + " (heldag)"
                : calendarEvent.StartTime.ToString("yyyy-MM-dd HH:mm");

            text.AppendLine($"""
                {i + 1}. {calendarEvent.Title}
                   Familjemedlem: {calendarEvent.FamilyMemberName}
                   Tid: {startStr}
                   Plats: {calendarEvent.Location ?? "–"}
                   Beskrivning: {calendarEvent.Description ?? "–"}

                   Godkänn: {approveUrl}
                   Avvisa: {rejectUrl}
                   Granska i appen: {reviewUrl}
                """);
            text.AppendLine();

            html.AppendLine($"""
                <div style="border:1px solid #ddd;border-radius:4px;padding:12px;margin-bottom:16px">
                <h3 style="margin:0 0 8px">{calendarEvent.Title}</h3>
                <table>
                  <tr><td><b>Familjemedlem:</b></td><td>{calendarEvent.FamilyMemberName}</td></tr>
                  <tr><td><b>Tid:</b></td><td>{startStr}</td></tr>
                  <tr><td><b>Plats:</b></td><td>{calendarEvent.Location ?? "–"}</td></tr>
                  <tr><td><b>Beskrivning:</b></td><td>{calendarEvent.Description ?? "–"}</td></tr>
                </table>
                <p>
                  <a href="{approveUrl}" style="background:#4caf50;color:white;padding:8px 16px;text-decoration:none;border-radius:4px;margin-right:8px">✅ Godkänn</a>
                  <a href="{rejectUrl}" style="background:#f44336;color:white;padding:8px 16px;text-decoration:none;border-radius:4px;margin-right:8px">❌ Avvisa</a>
                  <a href="{reviewUrl}" style="background:#2196f3;color:white;padding:8px 16px;text-decoration:none;border-radius:4px">🔍 Granska i appen</a>
                </p>
                </div>
                """);
        }

        text.AppendLine($"Hela granskningskön: {queueUrl}");
        html.AppendLine($"""<p><a href="{queueUrl}">Öppna hela granskningskön</a></p>""");
        html.AppendLine("</body></html>");

        try
        {
            await gmailClient.SendEmailAsync(opt.RecipientEmail, subject, html.ToString(), text.ToString());
            logger.LogInformation("Review notification sent for {Count} event(s): {EventIds}",
                calendarEvents.Count, string.Join(", ", calendarEvents.Select(e => e.Id)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send review notification for {Count} event(s)", calendarEvents.Count);
        }
    }
}
