namespace Johodp.Application.Users.Commands.AssignScopeToUser;

using MediatR;
using FluentValidation;

public class AssignScopeToUserCommandValidator : AbstractValidator<AssignScopeToUserCommand>
{
    public AssignScopeToUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.ScopeId)
            .NotEmpty().WithMessage("Scope ID is required");
    }
}
