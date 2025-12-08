namespace Johodp.Infrastructure.Identity;

/// <summary>
/// Helper for extracting tenant name from acr_values or request body, reusable across layers.
/// </summary>
public static class TenantClaimHelper
{
    public static string? ExtractTenantName(string? acrValues, string? requestTenantName)
    {
        if (!string.IsNullOrEmpty(acrValues) && acrValues.StartsWith("tenant:", System.StringComparison.OrdinalIgnoreCase))
            return acrValues.Substring(7);
        return requestTenantName;
    }
}
