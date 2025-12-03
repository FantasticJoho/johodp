namespace Johodp.Application.CustomConfigurations.Validators;

using Johodp.Application.CustomConfigurations.Commands;
using Johodp.Messaging.Validation;
using System.Text.RegularExpressions;

/// <summary>
/// Validates UpdateCustomConfigurationCommand input data
/// Note: Database checks (config exists, etc.) are done in HandleCore
/// </summary>
public class UpdateCustomConfigurationCommandValidator : IValidator<UpdateCustomConfigurationCommand>
{
    private static readonly Regex HexColorRegex = new(
        @"^#(?:[0-9a-fA-F]{3}){1,2}$",
        RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(
        @"^https?://[^\s/$.?#].[^\s]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LanguageCodeRegex = new(
        @"^[a-z]{2}(-[A-Z]{2})?$",
        RegexOptions.Compiled);

    public Task<IDictionary<string, string[]>> ValidateAsync(UpdateCustomConfigurationCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // Validate Id
        if (request.Id == Guid.Empty)
        {
            errors["Id"] = new[] { "Configuration ID is required" };
        }

        if (request.Data == null)
        {
            errors["Data"] = new[] { "Request data is required" };
            return Task.FromResult<IDictionary<string, string[]>>(errors);
        }

        // ✅ Validations synchrones uniquement

        // Validate Description (if provided)
        if (request.Data.Description != null)
        {
            if (request.Data.Description.Length > 500)
            {
                errors["Description"] = new[] { "Description cannot exceed 500 characters" };
            }
        }

        // Validate PrimaryColor (if provided)
        if (!string.IsNullOrWhiteSpace(request.Data.PrimaryColor) && 
            !HexColorRegex.IsMatch(request.Data.PrimaryColor))
        {
            errors["PrimaryColor"] = new[] { "Primary color must be a valid hex color (e.g., #FF5733)" };
        }

        // Validate SecondaryColor (if provided)
        if (!string.IsNullOrWhiteSpace(request.Data.SecondaryColor) && 
            !HexColorRegex.IsMatch(request.Data.SecondaryColor))
        {
            errors["SecondaryColor"] = new[] { "Secondary color must be a valid hex color (e.g., #FF5733)" };
        }

        // Validate LogoUrl (if provided)
        if (!string.IsNullOrWhiteSpace(request.Data.LogoUrl) && 
            !UrlRegex.IsMatch(request.Data.LogoUrl))
        {
            errors["LogoUrl"] = new[] { "Logo URL must be a valid HTTP/HTTPS URL" };
        }

        // Validate BackgroundImageUrl (if provided)
        if (!string.IsNullOrWhiteSpace(request.Data.BackgroundImageUrl) && 
            !UrlRegex.IsMatch(request.Data.BackgroundImageUrl))
        {
            errors["BackgroundImageUrl"] = new[] { "Background image URL must be a valid HTTP/HTTPS URL" };
        }

        // Validate CustomCss (if provided)
        if (!string.IsNullOrWhiteSpace(request.Data.CustomCss) && 
            request.Data.CustomCss.Length > 10000)
        {
            errors["CustomCss"] = new[] { "Custom CSS cannot exceed 10000 characters" };
        }

        // Validate DefaultLanguage (if provided)
        if (!string.IsNullOrWhiteSpace(request.Data.DefaultLanguage) && 
            !LanguageCodeRegex.IsMatch(request.Data.DefaultLanguage))
        {
            errors["DefaultLanguage"] = new[] { 
                "Default language must be a valid language code (e.g., 'fr' or 'fr-FR')" 
            };
        }

        // Validate SupportedLanguages (if provided)
        if (request.Data.SupportedLanguages != null && request.Data.SupportedLanguages.Any())
        {
            var invalidLanguages = request.Data.SupportedLanguages
                .Where(lang => !string.IsNullOrWhiteSpace(lang) && !LanguageCodeRegex.IsMatch(lang))
                .ToList();

            if (invalidLanguages.Any())
            {
                errors["SupportedLanguages"] = new[] { 
                    $"Invalid language codes: {string.Join(", ", invalidLanguages)}" 
                };
            }
        }

        // ❌ PAS de check DB ici (config exists, etc.)
        // → Ces validations sont faites dans HandleCore avec Result pattern

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }
}
