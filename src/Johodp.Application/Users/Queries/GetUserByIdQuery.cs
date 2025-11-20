namespace Johodp.Application.Users.Queries;

using Johodp.Application.Users.DTOs;
using Johodp.Application.Common.Mediator;

public class GetUserByIdQuery : IRequest<UserDto>
{
    public Guid UserId { get; set; }

    public GetUserByIdQuery(Guid userId)
    {
        UserId = userId;
    }
}
