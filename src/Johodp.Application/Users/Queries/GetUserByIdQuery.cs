namespace Johodp.Application.Users.Queries;

using MediatR;
using Johodp.Application.Users.DTOs;

public class GetUserByIdQuery : IRequest<UserDto>
{
    public Guid UserId { get; set; }

    public GetUserByIdQuery(Guid userId)
    {
        UserId = userId;
    }
}
