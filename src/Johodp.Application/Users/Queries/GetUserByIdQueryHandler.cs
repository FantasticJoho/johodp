namespace Johodp.Application.Users.Queries;

using Johodp.Contracts.Users;
using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Mediator;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Results;
using Microsoft.Extensions.Logging;

public class GetUserByIdQueryHandler : BaseHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetUserByIdQueryHandler> logger) : base(logger)
    {
        _unitOfWork = unitOfWork;
    }

    protected override async Task<Result<UserDto>> HandleCore(GetUserByIdQuery request, CancellationToken cancellationToken)
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
