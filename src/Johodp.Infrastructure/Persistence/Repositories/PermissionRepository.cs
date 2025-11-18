namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence.DbContext;

public class PermissionRepository : IPermissionRepository
{
    private readonly JohodpDbContext _context;

    public PermissionRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<Permission?> GetByIdAsync(PermissionId id)
    {
        return await _context.Permissions.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Permission?> GetByNameAsync(PermissionName name)
    {
        return await _context.Permissions.FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<Permission> AddAsync(Permission permission)
    {
        await _context.Permissions.AddAsync(permission);
        return permission;
    }

    public async Task<Permission> UpdateAsync(Permission permission)
    {
        _context.Permissions.Update(permission);
        return await Task.FromResult(permission);
    }

    public async Task<bool> DeleteAsync(PermissionId id)
    {
        var permission = await GetByIdAsync(id);
        if (permission == null)
            return false;

        _context.Permissions.Remove(permission);
        return true;
    }
}
