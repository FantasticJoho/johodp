namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence.DbContext;

public class UserRepository : IUserRepository
{
    private readonly JohodpDbContext _context;

    public UserRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(UserId id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var emailVo = Johodp.Domain.Users.ValueObjects.Email.Create(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == emailVo);
    }

    public async Task<User?> GetByEmailAndTenantAsync(string email, TenantId tenantId)
    {
        var emailVo = Johodp.Domain.Users.ValueObjects.Email.Create(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == emailVo && u.UserTenants.Any(ut => ut.TenantId == tenantId));
    }

    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return await Task.FromResult(user);
    }

    public async Task<bool> DeleteAsync(UserId id)
    {
        var user = await GetByIdAsync(id);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        return true;
    }
}
