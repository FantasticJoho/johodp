namespace Johodp.Application.Users.Commands;

using MediatR;

public class RegisterUserCommand : IRequest<RegisterUserResponse>
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}

public class RegisterUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
}
