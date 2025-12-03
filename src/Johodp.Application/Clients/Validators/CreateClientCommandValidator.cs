namespace Johodp.Application.Clients.Validators;

using Johodp.Application.Clients.Commands;
using Johodp.Messaging.Validation;

/// <summary>
/// Validates CreateClientCommand input data
/// Note: Database checks (name exists, etc.) are done in HandleCore for performance
/// </summary>
public class CreateClientCommandValidator : IValidator<CreateClientCommand>
{
    public Task<IDictionary<string, string[]>> ValidateAsync(CreateClientCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Data == null)
        {
            errors["Data"] = new[] { "Request data is required" };
            return Task.FromResult<IDictionary<string, string[]>>(errors);
        }

        // ✅ Validations synchrones uniquement (format, longueur, règles simples)
        
        // Validate ClientName
        if (string.IsNullOrWhiteSpace(request.Data.ClientName))
        {
            errors["ClientName"] = new[] { "Client name is required" };
        }
        else if (request.Data.ClientName.Length < 3)
        {
            errors["ClientName"] = new[] { "Client name must be at least 3 characters long" };
        }
        else if (request.Data.ClientName.Length > 100)
        {
            errors["ClientName"] = new[] { "Client name cannot exceed 100 characters" };
        }
        else if (!IsValidClientName(request.Data.ClientName))
        {
            errors["ClientName"] = new[] { 
                "Client name can only contain letters, numbers, hyphens, and underscores" 
            };
        }

        // Validate AllowedScopes
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

        // ❌ PAS de check DB ici (client exists, tenant exists, etc.)
        // → Ces validations métier sont faites dans HandleCore avec Result pattern
        // → Évite les double round-trips et les race conditions

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }

    private static bool IsValidClientName(string name)
    {
        // Alphanumeric, hyphen, underscore only
        return name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}
