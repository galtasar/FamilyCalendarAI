using FamilyCalendar.Core.Interfaces;
using FamilyCalendar.Core.Models;
using FamilyCalendar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyCalendar.Infrastructure.Repositories;

public class FamilyMemberRepository(AppDbContext db) : IFamilyMemberRepository
{
    public async Task<IReadOnlyList<FamilyMember>> GetAllAsync(CancellationToken ct = default) =>
        await db.FamilyMembers.OrderBy(c => c.Name).ToListAsync(ct);

    public Task<FamilyMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.FamilyMembers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<FamilyMember?> GetByNameAsync(string name, CancellationToken ct = default) =>
        db.FamilyMembers.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), ct);

    public async Task UpdateAsync(FamilyMember familyMember, CancellationToken ct = default)
    {
        db.FamilyMembers.Update(familyMember);
        await db.SaveChangesAsync(ct);
    }
}
