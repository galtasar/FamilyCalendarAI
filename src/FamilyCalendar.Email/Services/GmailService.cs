using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Email.Services;

public class GmailOptions
{
    public const string Section = "Gmail";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = "me";
    public string ApplicationEmail { get; set; } = "dahl.aicalendar@gmail.com";
    public string ProcessedLabelName { get; set; } = "FamilyCalendarProcessed";
}

public class GmailClientService(IOptions<GmailOptions> options, ILogger<GmailClientService> logger)
{
    private GmailService? _client;

    public async Task<GmailService> GetClientAsync()
    {
        if (_client != null) return _client;

        var opt = options.Value;
        var flow = new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow(
            new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = opt.ClientId, ClientSecret = opt.ClientSecret },
                Scopes = [GmailService.Scope.GmailReadonly, GmailService.Scope.GmailModify]
            });

        var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse { RefreshToken = opt.RefreshToken };
        var credential = new UserCredential(flow, opt.UserId, token);

        _client = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FamilyCalendarAI"
        });

        logger.LogInformation("Gmail client initialized for {User}", opt.ApplicationEmail);
        return _client;
    }

    public async Task<string> EnsureLabelExistsAsync(string labelName)
    {
        var client = await GetClientAsync();
        var labels = await client.Users.Labels.List(options.Value.UserId).ExecuteAsync();
        var existing = labels.Labels?.FirstOrDefault(l => l.Name == labelName);
        if (existing != null) return existing.Id!;

        var created = await client.Users.Labels.Create(new Label { Name = labelName }, options.Value.UserId).ExecuteAsync();
        return created.Id!;
    }

    public async Task ApplyLabelAsync(string messageId, string labelId)
    {
        var client = await GetClientAsync();
        var req = new ModifyMessageRequest
        {
            AddLabelIds = [labelId],
            RemoveLabelIds = ["UNREAD"]
        };
        await client.Users.Messages.Modify(req, options.Value.UserId, messageId).ExecuteAsync();
    }

    public async Task<IList<Message>?> ListUnprocessedMessagesAsync(string processedLabelId)
    {
        var client = await GetClientAsync();
        var req = client.Users.Messages.List(options.Value.UserId);
        req.Q = $"-label:{options.Value.ProcessedLabelName}";
        var result = await req.ExecuteAsync();
        return result.Messages;
    }

    public async Task<int> RemoveLabelFromAllAsync(string labelId)
    {
        var client = await GetClientAsync();
        var req = client.Users.Messages.List(options.Value.UserId);
        req.Q = $"label:{options.Value.ProcessedLabelName}";
        req.MaxResults = 500;

        int total = 0;
        do
        {
            var page = await req.ExecuteAsync();
            if (page.Messages == null || page.Messages.Count == 0) break;

            foreach (var msg in page.Messages)
            {
                await client.Users.Messages.Modify(
                    new ModifyMessageRequest { RemoveLabelIds = [labelId] },
                    options.Value.UserId, msg.Id).ExecuteAsync();
            }

            total += page.Messages.Count;
            req.PageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(req.PageToken));

        return total;
    }

    public async Task<Message> GetMessageAsync(string messageId)
    {
        var client = await GetClientAsync();
        return await client.Users.Messages.Get(options.Value.UserId, messageId).ExecuteAsync();
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, string textBody)
    {
        var client = await GetClientAsync();
        var from = options.Value.ApplicationEmail;

        var raw = BuildRawEmail(from, to, subject, htmlBody, textBody);
        var message = new Message { Raw = raw };
        await client.Users.Messages.Send(message, options.Value.UserId).ExecuteAsync();
    }

    private static string BuildRawEmail(string from, string to, string subject, string htmlBody, string textBody)
    {
        var boundary = Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        sb.AppendLine($"From: {from}");
        sb.AppendLine($"To: {to}");
        sb.AppendLine($"Subject: =?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine($"Content-Type: multipart/alternative; boundary=\"{boundary}\"");
        sb.AppendLine();
        sb.AppendLine($"--{boundary}");
        sb.AppendLine("Content-Type: text/plain; charset=utf-8");
        sb.AppendLine("Content-Transfer-Encoding: base64");
        sb.AppendLine();
        sb.AppendLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(textBody)));
        sb.AppendLine($"--{boundary}");
        sb.AppendLine("Content-Type: text/html; charset=utf-8");
        sb.AppendLine("Content-Transfer-Encoding: base64");
        sb.AppendLine();
        sb.AppendLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody)));
        sb.AppendLine($"--{boundary}--");

        return Base64UrlEncode(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').Replace("=", "");
}
