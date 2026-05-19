using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace FamilyCalendar.Infrastructure.Channels;

public class EmailProcessingChannel : IEmailProcessingChannel
{
    private readonly Channel<Email> _channel = Channel.CreateBounded<Email>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    public async Task WriteAsync(Email email, CancellationToken ct = default) =>
        await _channel.Writer.WriteAsync(email, ct);

    public async IAsyncEnumerable<Email> ReadAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var email in _channel.Reader.ReadAllAsync(ct))
            yield return email;
    }
}
