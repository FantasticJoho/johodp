namespace Johodp.Application.Clients.Validators;

using Johodp.Application.Clients.Commands;
using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Validation;

/// <summary>
/// Example: Validator with database access (for complex business rules)
/// ⚠️ Use this pattern ONLY when validation logic is complex and reusable
/// For simple uniqueness checks, prefer doing them in HandleCore with Result pattern
/// </summary>
public class CreateClientCommandValidatorWithDb : IValidator<CreateClientCommand>
{
    private readonly IClientRepository _clientRepository;
    private readonly ITenantRepository _tenantRepository;

    public CreateClientCommandValidatorWithDb(
        IClientRepository clientRepository,
        ITenantRepository tenantRepository)
    {
        _clientRepository = clientRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<IDictionary<string, string[]>> ValidateAsync(CreateClientCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Data == null)
        {
            errors["Data"] = new[] { "Request data is required" };
            return errors;
        }

        // ✅ Validations synchrones FIRST (fail-fast, no DB hit)
        if (string.IsNullOrWhiteSpace(request.Data.ClientName))
        {
            errors["ClientName"] = new[] { "Client name is required" };
            return errors; // Early return = no DB call
        }

        if (request.Data.ClientName.Length < 3 || request.Data.ClientName.Length > 100)
        {
            errors["ClientName"] = new[] { "Client name must be between 3 and 100 characters" };
            return errors;
        }

        // ⚠️ DB validations (only if basic validations pass)
        
        // Check if client name already exists
        var existingClient = await _clientRepository.GetByNameAsync(request.Data.ClientName);
        if (existingClient != null)
        {
            errors["ClientName"] = new[] { 
                $"A client with name '{request.Data.ClientName}' already exists" 
            };
        }

        // Note: Add other DB validations here if needed
        // Example: Check tenant exists, check user permissions, etc.

        return errors;
    }
}
