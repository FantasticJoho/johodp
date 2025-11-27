namespace Johodp.Application.Users.Commands;

using Johodp.Application.Common.Mediator;
using Johodp.Domain.Tenants.ValueObjects;

public class UserTenantAssignment
{
    public TenantId TenantId { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}

public class RegisterUserCommand : IRequest<RegisterUserResponse>
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    
    /// <summary>
    /// Legacy single tenant support (deprecated - use Tenants instead)
    /// </summary>
    public TenantId? TenantId { get; set; }
    
    /// <summary>
    /// List of tenant assignments with role and scope per tenant
    /// </summary>
    public List<UserTenantAssignment>? Tenants { get; set; }
    
    public bool CreateAsPending { get; set; } = false;
    public string? Password { get; set; }
}

public class RegisterUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
}
