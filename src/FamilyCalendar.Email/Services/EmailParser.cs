using FamilyCalendar.Core.Models;
using Google.Apis.Gmail.v1.Data;
using Ical.Net;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace FamilyCalendar.Email.Services;

public class EmailParser
{
    private const int MaxAttachmentTextLength = 2000;

    private static readonly Regex[] QuotedContentPatterns =
    [
        new(@"^Den \d", RegexOptions.Multiline | RegexOptions.Compiled),
        new(@"^On \w{3}[\w,. ]+wrote:", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^-{3,}\s*Vidarebefordrat meddelande", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^-{3,}\s*Ursprungligt meddelande", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^-{3,}\s*Original Message", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^Begin forwarded message", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^Från:.*\nSkickat:", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];
    public ParsedEmail Parse(Message message)
    {
        var headers = message.Payload?.Headers ?? [];
        var subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(inget ämne)";
        var from = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? string.Empty;
        var dateStr = headers.FirstOrDefault(h => h.Name == "Date")?.Value;

        var receivedAt = DateTimeOffset.TryParse(dateStr, out var dt) ? dt.ToUniversalTime() : DateTimeOffset.UtcNow;

        var body = ExtractBody(message.Payload);
        body = StripQuotedContent(body);
        var attachmentText = ExtractAttachments(message.Payload);

        if (!string.IsNullOrWhiteSpace(attachmentText))
            body = body + "\n\n--- Bilagor ---\n" + attachmentText;

        return new ParsedEmail
        {
            MessageId = message.Id ?? string.Empty,
            Sender = from,
            Subject = subject,
            Body = body,
            ReceivedAt = receivedAt
        };
    }

    private static string ExtractBody(MessagePart? part)
    {
        if (part == null) return string.Empty;

        if (part.MimeType == "text/plain" && part.Body?.Data != null)
            return DecodeBase64(part.Body.Data);

        if (part.Parts != null)
        {
            var plain = part.Parts.FirstOrDefault(p => p.MimeType == "text/plain");
            if (plain?.Body?.Data != null) return DecodeBase64(plain.Body.Data);

            foreach (var child in part.Parts)
            {
                var result = ExtractBody(child);
                if (!string.IsNullOrEmpty(result)) return result;
            }
        }

        if (part.MimeType == "text/html" && part.Body?.Data != null)
            return StripHtml(DecodeBase64(part.Body.Data));

        return string.Empty;
    }

    private static string ExtractAttachments(MessagePart? part)
    {
        if (part == null) return string.Empty;

        var sb = new StringBuilder();
        CollectAttachments(part, sb);
        return sb.ToString().Trim();
    }

    private static void CollectAttachments(MessagePart part, StringBuilder sb)
    {
        var filename = part.Filename ?? string.Empty;
        var mimeType = part.MimeType ?? string.Empty;
        var data = part.Body?.Data;

        if (!string.IsNullOrEmpty(data))
        {
            if (mimeType == "text/calendar" || filename.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
            {
                var icsText = TryParseIcs(DecodeBase64(data));
                if (!string.IsNullOrWhiteSpace(icsText))
                {
                    if (icsText.Length > MaxAttachmentTextLength) icsText = icsText[..MaxAttachmentTextLength] + "…";
                    sb.AppendLine($"[ICS-bilaga: {filename}]\n{icsText}");
                }
            }
            else if (mimeType == "application/pdf" || filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdfText = TryExtractPdf(Convert.FromBase64String(
                    data.Replace('-', '+').Replace('_', '/')));
                if (!string.IsNullOrWhiteSpace(pdfText))
                {
                    if (pdfText.Length > MaxAttachmentTextLength) pdfText = pdfText[..MaxAttachmentTextLength] + "…";
                    sb.AppendLine($"[PDF-bilaga: {filename}]\n{pdfText}");
                }
            }
        }

        if (part.Parts != null)
            foreach (var child in part.Parts)
                CollectAttachments(child, sb);
    }

    private static string TryParseIcs(string icsContent)
    {
        try
        {
            var calendar = Calendar.Load(icsContent);
            var sb = new StringBuilder();
            foreach (var evt in calendar.Events.OfType<Ical.Net.CalendarComponents.CalendarEvent>())
            {
                sb.AppendLine($"Händelse: {evt.Summary}");
                if (evt.DtStart != null) sb.AppendLine($"Start: {evt.DtStart.Value}");
                if (evt.DtEnd != null) sb.AppendLine($"Slut: {evt.DtEnd.Value}");
                if (!string.IsNullOrWhiteSpace(evt.Location)) sb.AppendLine($"Plats: {evt.Location}");
                if (!string.IsNullOrWhiteSpace(evt.Description)) sb.AppendLine($"Beskrivning: {evt.Description}");
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return icsContent; // fall back to raw text
        }
    }

    private static string TryExtractPdf(byte[] pdfBytes)
    {
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(string.Join(" ", page.GetWords().Select(w => w.Text)));
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string StripHtml(string html)
    {
        var result = Regex.Replace(html, @"<br\s*/?>|</p>|</div>|</li>|</tr>", "\n", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<[^>]+>", " ");
        result = result
            .Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<")
            .Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&#39;", "'");
        result = Regex.Replace(result, @"[ \t]+", " ");
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim();
    }

    private static string StripQuotedContent(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;
        var earliest = int.MaxValue;
        foreach (var pattern in QuotedContentPatterns)
        {
            var match = pattern.Match(body);
            if (match.Success && match.Index < earliest)
                earliest = match.Index;
        }
        return earliest < int.MaxValue ? body[..earliest].TrimEnd() : body;
    }

    private static string DecodeBase64(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        var padding = base64.Length % 4;
        if (padding > 0) base64 += new string('=', 4 - padding);
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
