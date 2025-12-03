namespace Johodp.Application.Tenants.Validators;

using Johodp.Application.Tenants.Commands;
using Johodp.Messaging.Validation;
using System.Text.RegularExpressions;

/// <summary>
/// Validates CreateTenantCommand input data
/// Note: Database checks (name exists, client exists, etc.) are done in HandleCore
/// </summary>
public class CreateTenantCommandValidator : IValidator<CreateTenantCommand>
{
    private static readonly Regex UrlRegex = new(
        @"^https?://[^\s/$.?#].[^\s]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<IDictionary<string, string[]>> ValidateAsync(CreateTenantCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Data == null)
        {
            errors["Data"] = new[] { "Request data is required" };
            return Task.FromResult<IDictionary<string, string[]>>(errors);
        }

        // ✅ Validations synchrones uniquement (format, longueur, règles simples)

        // Validate Name
        if (string.IsNullOrWhiteSpace(request.Data.Name))
        {
            errors["Name"] = new[] { "Tenant name is required" };
        }
        else if (request.Data.Name.Length < 3)
        {
            errors["Name"] = new[] { "Tenant name must be at least 3 characters" };
        }
        else if (request.Data.Name.Length > 100)
        {
            errors["Name"] = new[] { "Tenant name cannot exceed 100 characters" };
        }
        else if (!IsValidTenantName(request.Data.Name))
        {
            errors["Name"] = new[] { 
                "Tenant name can only contain lowercase letters, numbers, and hyphens" 
            };
        }

        // Validate DisplayName
        if (string.IsNullOrWhiteSpace(request.Data.DisplayName))
        {
            errors["DisplayName"] = new[] { "Display name is required" };
        }
        else if (request.Data.DisplayName.Length < 3)
        {
            errors["DisplayName"] = new[] { "Display name must be at least 3 characters" };
        }
        else if (request.Data.DisplayName.Length > 200)
        {
            errors["DisplayName"] = new[] { "Display name cannot exceed 200 characters" };
        }

        // Validate CustomConfigurationId
        if (request.Data.CustomConfigurationId == Guid.Empty)
        {
            errors["CustomConfigurationId"] = new[] { "CustomConfigurationId is required" };
        }

        // Validate ClientId
        if (string.IsNullOrWhiteSpace(request.Data.ClientId))
        {
            errors["ClientId"] = new[] { "ClientId is required" };
        }
        else if (request.Data.ClientId.Length > 100)
        {
            errors["ClientId"] = new[] { "ClientId cannot exceed 100 characters" };
        }

        // Validate AllowedReturnUrls
        if (request.Data.AllowedReturnUrls != null && request.Data.AllowedReturnUrls.Any())
        {
            var invalidUrls = request.Data.AllowedReturnUrls
                .Where(url => !string.IsNullOrWhiteSpace(url) && !UrlRegex.IsMatch(url))
                .ToList();

            if (invalidUrls.Any())
            {
                errors["AllowedReturnUrls"] = new[] { 
                    $"Invalid URLs found: {string.Join(", ", invalidUrls)}" 
                };
            }
        }

        // Validate AllowedCorsOrigins
        if (request.Data.AllowedCorsOrigins != null && request.Data.AllowedCorsOrigins.Any())
        {
            var invalidOrigins = request.Data.AllowedCorsOrigins
                .Where(origin => !string.IsNullOrWhiteSpace(origin) && !UrlRegex.IsMatch(origin))
                .ToList();

            if (invalidOrigins.Any())
            {
                errors["AllowedCorsOrigins"] = new[] { 
                    $"Invalid origins found: {string.Join(", ", invalidOrigins)}" 
                };
            }
        }

        // ❌ PAS de check DB ici (tenant exists, client exists, config exists, etc.)
        // → Ces validations sont faites dans HandleCore avec Result pattern

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }

    private static bool IsValidTenantName(string name)
    {
        // Tenant name: lowercase, numbers, hyphens only
        return name.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-');
    }
}
