namespace Johodp.Application.Users.Queries;

using Johodp.Application.Users.DTOs;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken = default)
    {
        var userId = UserId.From(request.UserId);
        var user = await _unitOfWork.Users.GetByIdAsync(userId);

        if (user == null)
        {
            return Result<UserDto>.Failure(UserErrors.NotFound(request.UserId));
        }

        return Result<UserDto>.Success(new UserDto
        {
            Id = user.Id.Value,
            Email = user.Email.Value,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = user.EmailConfirmed,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });
    }
}
