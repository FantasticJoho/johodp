namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence.DbContext;

public class ScopeRepository : IScopeRepository
{
    private readonly JohodpDbContext _context;

    public ScopeRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<Scope?> GetByIdAsync(ScopeId id)
    {
        return await _context.Scopes.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Scope?> GetByCodeAsync(string code)
    {
        return await _context.Scopes.FirstOrDefaultAsync(s => s.Code == code.ToUpperInvariant());
    }

    public async Task<Scope> AddAsync(Scope scope)
    {
        await _context.Scopes.AddAsync(scope);
        return scope;
    }

    public async Task<Scope> UpdateAsync(Scope scope)
    {
        _context.Scopes.Update(scope);
        return await Task.FromResult(scope);
    }

    public async Task<bool> DeleteAsync(ScopeId id)
    {
        var scope = await GetByIdAsync(id);
        if (scope == null)
            return false;

        _context.Scopes.Remove(scope);
        return true;
    }
}
