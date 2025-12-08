namespace Johodp.Domain.Users.Entities;

using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

/// <summary>
/// Association entre un utilisateur et un tenant, avec rôle spécifique.
/// </summary>
public class UserTenant
{
    public UserId UserId { get; set; } = null!;
    public TenantId TenantId { get; set; } = null!;
    public string Role { get; set; } = "User";
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sous-périmètres d'accès dans le tenant (stockés en JSON)
    /// </summary>
    public List<string> SubScopes { get; set; } = new();

    public User? User { get; set; }
    public Tenant? Tenant { get; set; }
}
