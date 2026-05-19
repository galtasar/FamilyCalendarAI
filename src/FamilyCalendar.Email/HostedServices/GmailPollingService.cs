using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Email.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyCalendar.Email.HostedServices;

public class GmailPollingService(
    IServiceScopeFactory scopeFactory,
    GmailClientService gmailClient,
    EmailParser emailParser,
    IOptions<GmailOptions> options,
    ILogger<GmailPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Gmail polling service started. Interval: {Interval}", PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Gmail polling");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var labelId = await gmailClient.EnsureLabelExistsAsync(options.Value.ProcessedLabelName);
        var messages = await gmailClient.ListUnprocessedMessagesAsync(labelId);

        if (messages == null || messages.Count == 0)
        {
            logger.LogInformation("No new emails found in Gmail");
            return;
        }

        logger.LogInformation("Found {Count} unprocessed emails", messages.Count);

        using var scope = scopeFactory.CreateScope();
        var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
        var processingChannel = scope.ServiceProvider.GetRequiredService<IEmailProcessingChannel>();

        foreach (var msg in messages)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await ProcessMessageAsync(msg.Id, labelId, emailRepo, processingChannel, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message {MessageId}", msg.Id);
            }
        }
    }

    private async Task ProcessMessageAsync(
        string messageId, string labelId,
        IEmailRepository emailRepo,
        IEmailProcessingChannel channel,
        CancellationToken ct)
    {
        if (await emailRepo.ExistsByMessageIdAsync(messageId, ct))
        {
            logger.LogDebug("Message {MessageId} already processed, skipping", messageId);
            await gmailClient.ApplyLabelAsync(messageId, labelId);
            return;
        }

        var message = await gmailClient.GetMessageAsync(messageId);
        var parsed = emailParser.Parse(message);

        // Skip self-sent emails (system emails sent to itself)
        if (parsed.Sender.Contains(options.Value.ApplicationEmail, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Skipping self-sent message {MessageId}", messageId);
            await gmailClient.ApplyLabelAsync(messageId, labelId);
            return;
        }

        var email = new FamilyCalendar.Core.Models.Email
        {
            Id = Guid.NewGuid(),
            MessageId = parsed.MessageId,
            Sender = parsed.Sender,
            Subject = parsed.Subject,
            Body = parsed.Body,
            ReceivedAt = parsed.ReceivedAt,
            Classification = Core.Enums.EmailClassification.Pending
        };

        await emailRepo.AddAsync(email, ct);
        await channel.WriteAsync(email, ct);
        await gmailClient.ApplyLabelAsync(messageId, labelId);

        logger.LogInformation("Queued email {MessageId} for processing", messageId);
    }
}
