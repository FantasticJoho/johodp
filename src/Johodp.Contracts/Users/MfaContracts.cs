namespace Johodp.Contracts.Users;

/// <summary>
/// Response after TOTP enrollment
/// </summary>
public class TotpEnrollmentResponse
{
    public string SharedKey { get; set; } = null!;
    public string QrCodeUri { get; set; } = null!;
    public string ManualEntryKey { get; set; } = null!;
}

/// <summary>
/// Request to verify TOTP code during enrollment
/// </summary>
public class VerifyTotpRequest
{
    public string Code { get; set; } = null!;
}

/// <summary>
/// Request to verify TOTP code during login
/// </summary>
public class LoginWithTotpRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? TotpCode { get; set; }
    public string? TenantUrl { get; set; }
}

/// <summary>
/// Response indicating MFA is required
/// </summary>
public class MfaRequiredResponse
{
    public bool MfaRequired { get; set; }
    public string? MfaMethod { get; set; } // "totp"
    public string Message { get; set; } = null!;
}

/// <summary>
/// Request to verify TOTP code with pending_mfa cookie (Parcours 2)
/// </summary>
public class VerifyMfaRequest
{
    public string TotpCode { get; set; } = null!;
}

/// <summary>
/// Response after MFA status check
/// </summary>
public class MfaStatusResponse
{
    public bool MfaEnabled { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public int RecoveryCodesRemaining { get; set; }
    public bool IsMfaRequired { get; set; }
    public bool ClientRequiresMfa { get; set; }
}

/// <summary>
/// Request to initiate lost device recovery (Parcours 3 - Step 1)
/// </summary>
public class LostDeviceRequest
{
    public string Email { get; set; } = null!;
}

/// <summary>
/// Request to verify user identity (Parcours 3 - Step 2)
/// </summary>
public class VerifyIdentityRequest
{
    public string Token { get; set; } = null!;
    public Dictionary<string, string>? SecurityAnswers { get; set; }
}

/// <summary>
/// Response after identity verification
/// </summary>
public class VerifyIdentityResponse
{
    public string VerifiedToken { get; set; } = null!;
    public string ExpiresIn { get; set; } = null!;
    public string Message { get; set; } = null!;
}

/// <summary>
/// Request to reset MFA enrollment (Parcours 3 - Step 3)
/// </summary>
public class ResetEnrollmentRequest
{
    public string VerifiedToken { get; set; } = null!;
}

/// <summary>
/// Request to disable MFA (optional - if Client.RequireMfa = false)
/// </summary>
public class DisableMfaRequest
{
    public string Password { get; set; } = null!;
}

/// <summary>
/// Data stored in pending_mfa cookie
/// </summary>
public class PendingMfaData
{
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }
    public DateTime CreatedAt { get; set; }
}
