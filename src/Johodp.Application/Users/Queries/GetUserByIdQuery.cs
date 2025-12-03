namespace Johodp.Application.Users.Queries;

using Johodp.Contracts.Users;
using Johodp.Messaging.Mediator;
using Johodp.Application.Common.Results;

public class GetUserByIdQuery : IRequest<Result<UserDto>>
{
    public Guid UserId { get; set; }

    public GetUserByIdQuery(Guid userId)
    {
        UserId = userId;
    }
}
