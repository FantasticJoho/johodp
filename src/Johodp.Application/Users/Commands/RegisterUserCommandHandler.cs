namespace Johodp.Application.Users.Commands;

using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<RegisterUserResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventPublisher _domainEventPublisher;

    public RegisterUserCommandHandler(
        IUnitOfWork unitOfWork, 
        IDomainEventPublisher domainEventPublisher)
    {
        _unitOfWork = unitOfWork;
        _domainEventPublisher = domainEventPublisher;
    }

    public async Task<Result<RegisterUserResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken = default)
    {
        // Check if (email, tenantId) combination already exists
        var existingUser = await _unitOfWork.Users.GetByEmailAndTenantAsync(request.Email, request.TenantId);
        if (existingUser != null)
        {
            return Result<RegisterUserResponse>.Failure(UserErrors.AlreadyExistsForTenant(request.Email));
        }

        // Create user aggregate with single tenant, role, and scope
        var user = User.Create(
            request.Email, 
            request.FirstName, 
            request.LastName, 
            request.TenantId,
            request.Role,
            request.Scope,
            request.CreateAsPending);

        // If not pending and password provided, set it (for direct registration)
        // Note: Password hashing should be handled by UserManager in the API layer
        if (!request.CreateAsPending && !string.IsNullOrEmpty(request.Password))
        {
            user.SetPasswordHash(request.Password);
        }

        // Add to repository
        await _unitOfWork.Users.AddAsync(user);

        // Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish domain events
        await _domainEventPublisher.PublishAsync(user.DomainEvents, cancellationToken);

        // Clear events after publishing
        user.ClearDomainEvents();

        return Result<RegisterUserResponse>.Success(new RegisterUserResponse
        {
            UserId = user.Id.Value,
            Email = user.Email.Value
        });
    }
}
