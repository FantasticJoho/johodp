namespace Johodp.Application.Users.Commands.AssignScopeToUser;

using MediatR;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;

public class AssignScopeToUserCommandHandler : IRequestHandler<AssignScopeToUserCommand, AssignScopeToUserResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public AssignScopeToUserCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AssignScopeToUserResponse> Handle(AssignScopeToUserCommand request, CancellationToken cancellationToken)
    {
        // Récupérer l'utilisateur
        var userId = UserId.From(request.UserId);
        var user = await _unitOfWork.Users.GetByIdAsync(userId);

        if (user == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found");

        // Récupérer le périmètre
        var scopeId = ScopeId.From(request.ScopeId);
        var scope = await _unitOfWork.Scopes.GetByIdAsync(scopeId);

        if (scope == null)
            throw new KeyNotFoundException($"Scope with ID {request.ScopeId} not found");

        // Assigner le périmètre
        user.SetScope(scope);

        // Sauvegarder
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignScopeToUserResponse
        {
            UserId = user.Id.Value,
            ScopeCode = scope.Code,
            Message = $"Scope {scope.Name} assigned to user {user.Email.Value}"
        };
    }
}
