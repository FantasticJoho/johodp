namespace Johodp.Application.Users.Commands;

using Johodp.Application.Common.Mediator;

public class RegisterUserCommand : IRequest<RegisterUserResponse>
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? TenantId { get; set; }
    public bool CreateAsPending { get; set; } = false;
    public string? Password { get; set; }
}

public class RegisterUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
}
