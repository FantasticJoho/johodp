namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(PermissionId id);
    Task<Permission?> GetByNameAsync(PermissionName name);
    Task<Permission> AddAsync(Permission permission);
    Task<Permission> UpdateAsync(Permission permission);
    Task<bool> DeleteAsync(PermissionId id);
}
