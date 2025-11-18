namespace Johodp.Application.Users.Commands.AssignRoleToUser;

using MediatR;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Application.Common.Interfaces;

public class AssignRoleToUserCommandHandler : IRequestHandler<AssignRoleToUserCommand, AssignRoleToUserResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public AssignRoleToUserCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AssignRoleToUserResponse> Handle(AssignRoleToUserCommand request, CancellationToken cancellationToken)
    {
        // Récupérer l'utilisateur
        var userId = UserId.From(request.UserId);
        var user = await _unitOfWork.Users.GetByIdAsync(userId);

        if (user == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found");

        // Récupérer le rôle
        var roleId = RoleId.From(request.RoleId);
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);

        if (role == null)
            throw new KeyNotFoundException($"Role with ID {request.RoleId} not found");

        // Assigner le rôle
        user.AddRole(role);

        // Sauvegarder
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignRoleToUserResponse
        {
            UserId = user.Id.Value,
            Message = $"Role {role.Name} assigned to user {user.Email.Value}"
        };
    }
}
