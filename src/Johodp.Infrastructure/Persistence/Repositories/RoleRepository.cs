namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence.DbContext;

public class RoleRepository : IRoleRepository
{
    private readonly JohodpDbContext _context;

    public RoleRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(RoleId id)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Role?> GetByNameAsync(string name)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Name == name);
    }

    public async Task<Role> AddAsync(Role role)
    {
        await _context.Roles.AddAsync(role);
        return role;
    }

    public async Task<Role> UpdateAsync(Role role)
    {
        _context.Roles.Update(role);
        return await Task.FromResult(role);
    }

    public async Task<bool> DeleteAsync(RoleId id)
    {
        var role = await GetByIdAsync(id);
        if (role == null)
            return false;

        _context.Roles.Remove(role);
        return true;
    }
}
