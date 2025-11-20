namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Tenants.ValueObjects;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(TenantId id);
    Task<Tenant?> GetByNameAsync(string name);
    Task<IEnumerable<Tenant>> GetAllAsync();
    Task<IEnumerable<Tenant>> GetActiveTenantsAsync();
    Task<Tenant> AddAsync(Tenant tenant);
    Task<Tenant> UpdateAsync(Tenant tenant);
    Task<bool> DeleteAsync(TenantId id);
    Task<bool> ExistsAsync(string name);
}
