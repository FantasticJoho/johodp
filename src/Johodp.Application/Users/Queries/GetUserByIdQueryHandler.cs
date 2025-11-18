namespace Johodp.Application.Users.Queries;

using MediatR;
using Johodp.Application.Users.DTOs;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.UserId);
        var user = await _unitOfWork.Users.GetByIdAsync(userId);

        if (user == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found");

        return new UserDto
        {
            Id = user.Id.Value,
            Email = user.Email.Value,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = user.EmailConfirmed,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}
