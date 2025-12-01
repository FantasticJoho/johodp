namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Tenants.ValueObjects;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Domain.Users.ValueObjects.UserId id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailAndTenantAsync(string email, TenantId tenantId);
    Task<User> AddAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> DeleteAsync(Domain.Users.ValueObjects.UserId id);
}
