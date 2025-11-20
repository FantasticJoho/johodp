namespace Johodp.Application.Users.Commands;

using MediatR;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResponse>
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

    public async Task<RegisterUserResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Check if email already exists
        var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        if (existingUser != null)
            throw new InvalidOperationException($"User with email {request.Email} already exists");

        // Create user aggregate (with or without pending status)
        var user = User.Create(
            request.Email, 
            request.FirstName, 
            request.LastName, 
            request.TenantId,
            request.CreateAsPending);

        // If not pending and password provided, set it (for direct registration)
        // Note: Password hashing should be handled by UserManager in the API layer
        // This is just for the domain model
        if (!request.CreateAsPending && !string.IsNullOrEmpty(request.Password))
        {
            // Password will be hashed by Identity UserManager in the API layer
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

        return new RegisterUserResponse
        {
            UserId = user.Id.Value,
            Email = user.Email.Value
        };
    }
}
