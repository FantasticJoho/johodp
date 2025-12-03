namespace Johodp.Application.Clients.Validators;

using Johodp.Application.Clients.Commands;
using Johodp.Messaging.Validation;

/// <summary>
/// Validates UpdateClientCommand input data
/// Note: Database checks (client exists, tenant exists, etc.) are done in HandleCore
/// </summary>
public class UpdateClientCommandValidator : IValidator<UpdateClientCommand>
{
    public Task<IDictionary<string, string[]>> ValidateAsync(UpdateClientCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // Validate ClientId
        if (request.ClientId == Guid.Empty)
        {
            errors["ClientId"] = new[] { "ClientId is required" };
        }

        if (request.Data == null)
        {
            errors["Data"] = new[] { "Request data is required" };
            return Task.FromResult<IDictionary<string, string[]>>(errors);
        }

        // ✅ Validations synchrones uniquement

        // Validate AllowedScopes (if provided)
        if (request.Data.AllowedScopes != null && request.Data.AllowedScopes.Any())
        {
            var invalidScopes = request.Data.AllowedScopes
                .Where(s => string.IsNullOrWhiteSpace(s))
                .ToList();

            if (invalidScopes.Any())
            {
                errors["AllowedScopes"] = new[] { "Scopes cannot be empty or whitespace" };
            }

            var tooLongScopes = request.Data.AllowedScopes
                .Where(s => s?.Length > 50)
                .ToList();

            if (tooLongScopes.Any())
            {
                errors["AllowedScopes"] = new[] { "Each scope cannot exceed 50 characters" };
            }
        }

        // Validate AssociatedTenantIds (if provided)
        if (request.Data.AssociatedTenantIds != null && request.Data.AssociatedTenantIds.Any())
        {
            var invalidTenantIds = request.Data.AssociatedTenantIds
                .Where(id => string.IsNullOrWhiteSpace(id))
                .ToList();

            if (invalidTenantIds.Any())
            {
                errors["AssociatedTenantIds"] = new[] { "Tenant IDs cannot be empty" };
            }
        }

        // ❌ PAS de check DB ici (client exists, tenants exist, etc.)
        // → Ces validations sont faites dans HandleCore avec Result pattern

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }
}
