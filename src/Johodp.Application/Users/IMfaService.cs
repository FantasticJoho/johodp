using Johodp.Domain.Users;
using Johodp.Domain.Users.Aggregates;

namespace Johodp.Application.Users;

/// <summary>
/// Service responsible for MFA/TOTP-related business logic.
/// Encapsulates complex domain logic that would otherwise pollute the controller.
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Determines if MFA is required for a specific user.
    /// Traverses the chain: User -> Tenant -> Client to check Client.RequireMfa.
    /// </summary>
    /// <param name="user">The domain user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the user's tenant's client requires MFA, false otherwise</returns>
    Task<bool> IsMfaRequiredForUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a QR code URI for TOTP enrollment.
    /// Format: otpauth://totp/{issuer}:{accountIdentifier}?secret={secret}&issuer={issuer}
    /// </summary>
    /// <param name="email">User's email (account identifier)</param>
    /// <param name="unformattedKey">The shared secret key (base32)</param>
    /// <param name="issuer">The issuer name (application name)</param>
    /// <returns>QR code URI compatible with Google/Microsoft Authenticator</returns>
    string GenerateQrCodeUri(string email, string unformattedKey, string issuer);

    /// <summary>
    /// Formats a shared secret key for display to users.
    /// Adds spaces every 4 characters for readability (e.g., "ABCD EFGH IJKL").
    /// </summary>
    /// <param name="unformattedKey">The unformatted shared secret key</param>
    /// <returns>Formatted key with spaces</returns>
    string FormatKey(string unformattedKey);
}
