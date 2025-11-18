namespace Johodp.Application.Users.Commands.AssignScopeToUser;

using MediatR;

public class AssignScopeToUserCommand : IRequest<AssignScopeToUserResponse>
{
    public Guid UserId { get; set; }
    public Guid ScopeId { get; set; }
}

public class AssignScopeToUserResponse
{
    public Guid UserId { get; set; }
    public string ScopeCode { get; set; } = null!;
    public string Message { get; set; } = null!;
}
