namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

public interface IScopeRepository
{
    Task<Scope?> GetByIdAsync(ScopeId id);
    Task<Scope?> GetByCodeAsync(string code);
    Task<Scope> AddAsync(Scope scope);
    Task<Scope> UpdateAsync(Scope scope);
    Task<bool> DeleteAsync(ScopeId id);
}
