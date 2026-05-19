using FamilyCalendar.Core.Models;

namespace FamilyCalendar.Core.Interfaces;

public interface IFamilyMemberRepository
{
    Task<IReadOnlyList<FamilyMember>> GetAllAsync(CancellationToken ct = default);
    Task<FamilyMember?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FamilyMember?> GetByNameAsync(string name, CancellationToken ct = default);
    Task UpdateAsync(FamilyMember familyMember, CancellationToken ct = default);
}
