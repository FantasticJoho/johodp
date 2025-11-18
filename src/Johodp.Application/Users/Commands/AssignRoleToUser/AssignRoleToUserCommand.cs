namespace Johodp.Application.Users.Commands.AssignRoleToUser;

using MediatR;

public class AssignRoleToUserCommand : IRequest<AssignRoleToUserResponse>
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public class AssignRoleToUserResponse
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = null!;
}
