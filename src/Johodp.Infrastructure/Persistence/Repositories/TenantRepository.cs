namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Infrastructure.Persistence.DbContext;

public class TenantRepository : ITenantRepository
{
    private readonly JohodpDbContext _context;

    public TenantRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(TenantId id)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Tenant?> GetByNameAsync(string name)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Name == name.ToLowerInvariant());
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync()
    {
        return await _context.Tenants.ToListAsync();
    }

    public async Task<IEnumerable<Tenant>> GetActiveTenantsAsync()
    {
        return await _context.Tenants.Where(t => t.IsActive).ToListAsync();
    }

    public async Task<Tenant> AddAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
        return tenant;
    }

    public async Task<Tenant> UpdateAsync(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
        return await Task.FromResult(tenant);
    }

    public async Task<bool> DeleteAsync(TenantId id)
    {
        var tenant = await GetByIdAsync(id);
        if (tenant == null)
            return false;

        _context.Tenants.Remove(tenant);
        return true;
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _context.Tenants.AnyAsync(t => t.Name == name.ToLowerInvariant());
    }
}
