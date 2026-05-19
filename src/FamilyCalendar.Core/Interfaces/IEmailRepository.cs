using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IEmailRepository
{
    Task<bool> ExistsByMessageIdAsync(string messageId, CancellationToken ct = default);
    Task<Email> AddAsync(Email email, CancellationToken ct = default);
    Task UpdateAsync(Email email, CancellationToken ct = default);
    Task<Email?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Email>> GetRecentAsync(int count = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Email>> GetPendingAsync(CancellationToken ct = default);
}
