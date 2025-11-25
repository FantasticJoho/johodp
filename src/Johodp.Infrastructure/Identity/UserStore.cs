namespace Johodp.Infrastructure.Identity;

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

public class UserStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserTwoFactorStore<User>,
    IUserAuthenticatorKeyStore<User>,
    IUserTwoFactorRecoveryCodeStore<User>
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

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.Value.ToString());
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

    // Two-factor store
    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.SetTwoFactorEnabled(enabled);
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.TwoFactorEnabled);
    }

    // Authenticator key store
    public Task SetAuthenticatorKeyAsync(User user, string key, CancellationToken cancellationToken)
    {
        user.SetAuthenticatorKey(key);
        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.AuthenticatorKey);
    }

    // Interface expects ReplaceCodesAsync (naming in ASP.NET Identity)
    public Task ReplaceCodesAsync(User user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        user.ReplaceRecoveryCodes(recoveryCodes);
        return Task.CompletedTask;
    }

    // Backward compatible method expected by UserManager (ASP.NET Identity interface)
    public Task ReplaceRecoveryCodesAsync(User user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        return ReplaceCodesAsync(user, recoveryCodes, cancellationToken);
    }

    public Task<IEnumerable<string>> GetRecoveryCodesAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.RecoveryCodes.AsEnumerable());
    }

    public Task<int> CountCodesAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.RecoveryCodes.Count);
    }

    public Task<bool> RedeemCodeAsync(User user, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Task.FromResult(false);
        var match = user.RecoveryCodes.FirstOrDefault(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
        if (match == null) return Task.FromResult(false);
        var remaining = user.RecoveryCodes.Where(c => !string.Equals(c, code, StringComparison.OrdinalIgnoreCase)).ToList();
        user.ReplaceRecoveryCodes(remaining);
        return Task.FromResult(true);
    }
}
