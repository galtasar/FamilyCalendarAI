using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyCalendar.Infrastructure.Repositories;

public class EmailRepository(AppDbContext db) : IEmailRepository
{
    public Task<bool> ExistsByMessageIdAsync(string messageId, CancellationToken ct = default) =>
        db.Emails.AnyAsync(e => e.MessageId == messageId, ct);

    public async Task<Email> AddAsync(Email email, CancellationToken ct = default)
    {
        db.Emails.Add(email);
        await db.SaveChangesAsync(ct);
        return email;
    }

    public async Task UpdateAsync(Email email, CancellationToken ct = default)
    {
        db.Emails.Update(email);
        await db.SaveChangesAsync(ct);
    }

    public Task<Email?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Emails.Include(e => e.Events).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Email>> GetRecentAsync(int count = 50, CancellationToken ct = default) =>
        await db.Emails.OrderByDescending(e => e.ReceivedAt).Take(count).ToListAsync(ct);

    public async Task<IReadOnlyList<Email>> GetPendingAsync(CancellationToken ct = default) =>
        await db.Emails.Where(e => e.Classification == Core.Enums.EmailClassification.Pending).ToListAsync(ct);
}
