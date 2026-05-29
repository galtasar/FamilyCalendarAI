using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Email.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyCalendar.Email.HostedServices;

public class WeeklySummaryOptions
{
    public const string Section = "WeeklySummary";
    public int SendOnDayOfWeek { get; set; } = 0; // Sunday
    public int SendAtHour { get; set; } = 18;
    public string RecipientEmail { get; set; } = "dahl.aicalendar@gmail.com";
}

public class WeeklySummaryService(
    IServiceScopeFactory scopeFactory,
    GmailClientService gmailClient,
    IOptions<WeeklySummaryOptions> options,
    ILogger<WeeklySummaryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var nextSend = GetNextSendTime(now, options.Value);
            var delay = nextSend - now;

            logger.LogInformation("Weekly summary next send: {NextSend}", nextSend);
            await Task.Delay(delay, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
                await SendWeeklySummaryAsync(stoppingToken);
        }
    }

    private static DateTimeOffset GetNextSendTime(DateTimeOffset now, WeeklySummaryOptions opt)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var target = (DayOfWeek)opt.SendOnDayOfWeek;
        var candidate = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, opt.SendAtHour, 0, 0, nowLocal.Offset);
        var daysUntil = ((int)target - (int)nowLocal.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysUntil);
        if (candidate <= nowLocal)
            candidate = candidate.AddDays(7);
        return candidate.ToUniversalTime();
    }

    private async Task SendWeeklySummaryAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();

            var tz2 = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
            var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz2).Date;
            // Next Monday from today (always covers the week after the summary is sent)
            var daysToNextMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (daysToNextMonday == 0) daysToNextMonday = 7; // already Monday → next Monday
            var monday = today.AddDays(daysToNextMonday);
            var sunday = monday.AddDays(6).AddHours(23).AddMinutes(59);

            var events = await eventRepo.GetByDateRangeAsync(monday, sunday, null, ct);
            var pendingReview = await eventRepo.GetPendingReviewAsync(ct);

            var (html, text) = BuildSummary(events, pendingReview, monday);

            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(monday);
            var subject = $"Familjekalender – Vecka {weekNumber}";

            await gmailClient.SendEmailAsync(options.Value.RecipientEmail, subject, html, text);
            logger.LogInformation("Weekly summary sent for week {Week}", weekNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send weekly summary");
        }
    }

    private static (string Html, string Text) BuildSummary(
        IReadOnlyList<CalendarEvent> events,
        IReadOnlyList<CalendarEvent> pendingReview,
        DateTimeOffset weekStart)
    {
        var byDay = events.GroupBy(e => e.StartTime.Date).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.ToList());
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(weekStart.Date);

        var swedishDays = new[] { "Måndag", "Tisdag", "Onsdag", "Torsdag", "Fredag", "Lördag", "Söndag" };
        var sb = new System.Text.StringBuilder();
        var htmlSb = new System.Text.StringBuilder();

        sb.AppendLine($"Vecka {weekNumber}");
        sb.AppendLine();
        htmlSb.AppendLine($"<html><body><h1>Vecka {weekNumber}</h1>");

        for (int i = 0; i < 7; i++)
        {
            var day = weekStart.Date.AddDays(i);
            if (!byDay.TryGetValue(day, out var dayEvents)) continue;

            var dayName = swedishDays[i];
            sb.AppendLine(dayName);
            htmlSb.AppendLine($"<h2>{dayName} {day:dd/MM}</h2><ul>");

            foreach (var evt in dayEvents.OrderBy(e => e.StartTime))
            {
                var timeStr = evt.EndTime == null ? "" : $" kl {evt.StartTime:HH:mm}";
                sb.AppendLine($"  - {evt.FamilyMemberName}: {evt.Title}{timeStr}");
                htmlSb.AppendLine($"<li><b>{evt.FamilyMemberName}:</b> {evt.Title}{timeStr}</li>");
            }

            sb.AppendLine();
            htmlSb.AppendLine("</ul>");
        }

        if (pendingReview.Any())
        {
            sb.AppendLine("⚠ Behöver bekräftas");
            htmlSb.AppendLine("<h2>⚠ Behöver bekräftas</h2><ul>");
            foreach (var evt in pendingReview)
            {
                sb.AppendLine($"  - {evt.FamilyMemberName}: {evt.Title} ({evt.StartTime:yyyy-MM-dd})");
                htmlSb.AppendLine($"<li>{evt.FamilyMemberName}: {evt.Title} ({evt.StartTime:yyyy-MM-dd})</li>");
            }
            htmlSb.AppendLine("</ul>");
        }

        htmlSb.AppendLine("</body></html>");
        return (htmlSb.ToString(), sb.ToString());
    }
}
