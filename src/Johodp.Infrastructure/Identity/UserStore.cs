namespace Johodp.Infrastructure.Identity;

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

public class UserStore : IUserStore<User>, IUserPasswordStore<User>, IUserEmailStore<User>
{
    private readonly IUnitOfWork _unitOfWork;

    public UserStore(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        await _unitOfWork.Users.DeleteAsync(user.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public void Dispose() { }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var guid))
            return null;

        return await _unitOfWork.Users.GetByIdAsync(UserId.From(guid));
    }

    public async Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        // normalizedUserName expected to be email upper-case; use email lookup
        return await _unitOfWork.Users.GetByEmailAsync(normalizedUserName);
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value.ToUpperInvariant());
    }

    public Task<string?> GetUserIdAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Id.Value.ToString());
    }

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value);
    }

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        // no-op, domain user keeps its email
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        // no-op, username tied to email in this model
        return Task.CompletedTask;
    }

    // Password store
    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.SetPasswordHash(passwordHash);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
    }

    // Email store
    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        // Domain email is a value object; if you want to change it, implement a domain method.
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value);
    }

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        if (confirmed && !user.EmailConfirmed)
        {
            user.ConfirmEmail();
        }

        return Task.CompletedTask;
    }

    public async Task<User?> FindByEmailAsync(string? normalizedEmail, CancellationToken cancellationToken)
    {
        if (normalizedEmail == null) return null;
        return await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
    }

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value.ToUpperInvariant());
    }

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }
}
