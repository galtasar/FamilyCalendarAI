using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IEmailProcessingChannel
{
    Task WriteAsync(Email email, CancellationToken ct = default);
    IAsyncEnumerable<Email> ReadAllAsync(CancellationToken ct = default);
}
