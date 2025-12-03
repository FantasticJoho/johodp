namespace Johodp.Application.Tenants.Validators;

using Johodp.Application.Tenants.Commands;
using Johodp.Messaging.Validation;
using System.Text.RegularExpressions;

/// <summary>
/// Validates UpdateTenantCommand input data
/// Note: Database checks (tenant exists, client exists, etc.) are done in HandleCore
/// </summary>
public class UpdateTenantCommandValidator : IValidator<UpdateTenantCommand>
{
    private static readonly Regex UrlRegex = new(
        @"^https?://[^\s/$.?#].[^\s]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<IDictionary<string, string[]>> ValidateAsync(UpdateTenantCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // Validate TenantId
        if (request.TenantId == Guid.Empty)
        {
            errors["TenantId"] = new[] { "TenantId is required" };
        }

        if (request.Data == null)
        {
            errors["Data"] = new[] { "Request data is required" };
            return Task.FromResult<IDictionary<string, string[]>>(errors);
        }

        // ✅ Validations synchrones uniquement

        // Validate DisplayName (if provided)
        if (request.Data.DisplayName != null)
        {
            if (string.IsNullOrWhiteSpace(request.Data.DisplayName))
            {
                errors["DisplayName"] = new[] { "Display name cannot be empty" };
            }
            else if (request.Data.DisplayName.Length < 3)
            {
                errors["DisplayName"] = new[] { "Display name must be at least 3 characters" };
            }
            else if (request.Data.DisplayName.Length > 200)
            {
                errors["DisplayName"] = new[] { "Display name cannot exceed 200 characters" };
            }
        }

        // Validate CustomConfigurationId (if provided)
        if (request.Data.CustomConfigurationId.HasValue && 
            request.Data.CustomConfigurationId.Value == Guid.Empty)
        {
            errors["CustomConfigurationId"] = new[] { "CustomConfigurationId cannot be empty" };
        }

        // Validate ClientId (if provided)
        if (request.Data.ClientId != null)
        {
            if (string.IsNullOrWhiteSpace(request.Data.ClientId))
            {
                errors["ClientId"] = new[] { "ClientId cannot be empty" };
            }
            else if (request.Data.ClientId.Length > 100)
            {
                errors["ClientId"] = new[] { "ClientId cannot exceed 100 characters" };
            }
        }

        // Validate AllowedReturnUrls (if provided)
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

        // Validate AllowedCorsOrigins (if provided)
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
}
