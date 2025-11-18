namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId id);
    Task<Role?> GetByNameAsync(string name);
    Task<Role> AddAsync(Role role);
    Task<Role> UpdateAsync(Role role);
    Task<bool> DeleteAsync(RoleId id);
}
