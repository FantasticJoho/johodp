namespace Johodp.Application.Users.Commands;

using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Contracts.Users;
using Johodp.Domain.Tenants.ValueObjects;

public class RegisterUserCommand : IRequest<Result<RegisterUserResponse>>
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    
    /// <summary>
    /// TenantId is required - user belongs to a single tenant
    /// </summary>
    public TenantId TenantId { get; set; } = null!;
    
    /// <summary>
    /// Role assigned to the user (provided by external application)
    /// </summary>
    public string Role { get; set; } = "user";
    
    /// <summary>
    /// Scope assigned to the user (provided by external application)
    /// </summary>
    public string Scope { get; set; } = "default";
    
    public bool CreateAsPending { get; set; } = false;
    public string? Password { get; set; }
}
