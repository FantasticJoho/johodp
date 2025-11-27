namespace Johodp.Domain.Users.Entities;

using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

/// <summary>
/// Represents the relationship between a User and a Tenant with tenant-specific role and scope
/// </summary>
public class UserTenant
{
    public UserId UserId { get; private set; }
    public TenantId TenantId { get; private set; }
    
    /// <summary>
    /// Role assigned to the user for this specific tenant (provided by external application)
    /// </summary>
    public string Role { get; private set; }
    
    /// <summary>
    /// Scope/Perimeter assigned to the user for this specific tenant (provided by external application)
    /// </summary>
    public string Scope { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // EF Core constructor
#pragma warning disable CS8618
    private UserTenant()
    {
        Role = string.Empty;
        Scope = string.Empty;
    }
#pragma warning restore CS8618

    private UserTenant(UserId userId, TenantId tenantId, string role, string scope)
    {
        UserId = userId;
        TenantId = tenantId;
        Role = role;
        Scope = scope;
        CreatedAt = DateTime.UtcNow;
    }

    public static UserTenant Create(UserId userId, TenantId tenantId, string role, string scope)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));
        
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope cannot be empty", nameof(scope));

        return new UserTenant(userId, tenantId, role, scope);
    }

    public void UpdateRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope cannot be empty", nameof(scope));

        Scope = scope;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string role, string scope)
    {
        UpdateRole(role);
        UpdateScope(scope);
    }
}
