namespace Johodp.Application.Users.Validators;

using Johodp.Application.Users.Commands;
using Johodp.Messaging.Validation;
using System.Text.RegularExpressions;

/// <summary>
/// Validates RegisterUserCommand input data
/// Note: Tenant existence check is done in HandleCore, not here
/// </summary>
public class RegisterUserCommandValidator : IValidator<RegisterUserCommand>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<IDictionary<string, string[]>> ValidateAsync(RegisterUserCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // ✅ Validations synchrones uniquement

        // Validate Email
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["Email"] = new[] { "Email is required" };
        }
        else if (request.Email.Length > 256)
        {
            errors["Email"] = new[] { "Email cannot exceed 256 characters" };
        }
        else if (!EmailRegex.IsMatch(request.Email))
        {
            errors["Email"] = new[] { "Email format is invalid" };
        }

        // Validate FirstName
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors["FirstName"] = new[] { "First name is required" };
        }
        else if (request.FirstName.Length < 2)
        {
            errors["FirstName"] = new[] { "First name must be at least 2 characters" };
        }
        else if (request.FirstName.Length > 100)
        {
            errors["FirstName"] = new[] { "First name cannot exceed 100 characters" };
        }

        // Validate LastName
        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors["LastName"] = new[] { "Last name is required" };
        }
        else if (request.LastName.Length < 2)
        {
            errors["LastName"] = new[] { "Last name must be at least 2 characters" };
        }
        else if (request.LastName.Length > 100)
        {
            errors["LastName"] = new[] { "Last name cannot exceed 100 characters" };
        }

        // Validate TenantId
        if (request.TenantId == null)
        {
            errors["TenantId"] = new[] { "TenantId is required" };
        }

        // Validate Role
        if (string.IsNullOrWhiteSpace(request.Role))
        {
            errors["Role"] = new[] { "Role is required" };
        }
        else if (request.Role.Length > 50)
        {
            errors["Role"] = new[] { "Role cannot exceed 50 characters" };
        }

        // Validate Scope
        if (string.IsNullOrWhiteSpace(request.Scope))
        {
            errors["Scope"] = new[] { "Scope is required" };
        }
        else if (request.Scope.Length > 100)
        {
            errors["Scope"] = new[] { "Scope cannot exceed 100 characters" };
        }

        // Validate Password (if provided)
        if (!request.CreateAsPending && string.IsNullOrWhiteSpace(request.Password))
        {
            errors["Password"] = new[] { "Password is required when not creating as pending" };
        }
        else if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
        {
            errors["Password"] = new[] { "Password must be at least 8 characters" };
        }

        // ❌ PAS de check DB ici (tenant exists, email unique, etc.)
        // → Ces validations sont faites dans HandleCore avec Result pattern

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }
}
