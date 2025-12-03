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
