namespace Johodp.Application.CustomConfigurations;

using Johodp.Application.Common.Results;

/// <summary>
/// Centralized error messages for CustomConfiguration operations
/// </summary>
public static class CustomConfigurationErrors
{
    // Conflict errors
    public static Error AlreadyExists(string configName) => Error.Conflict(
        "CUSTOM_CONFIG_ALREADY_EXISTS",
        $"A CustomConfiguration with name '{configName}' already exists");

    // NotFound errors
    public static Error NotFound(Guid configId) => Error.NotFound(
        "CUSTOM_CONFIG_NOT_FOUND",
        $"CustomConfiguration with ID '{configId}' not found");

    public static Error NotFoundByName(string configName) => Error.NotFound(
        "CUSTOM_CONFIG_NOT_FOUND",
        $"CustomConfiguration with name '{configName}' not found");

    // Validation errors
    public static Error NameRequired() => Error.Validation(
        "CONFIG_NAME_REQUIRED",
        "CustomConfiguration name is required");

    public static Error InvalidLanguageCode(string languageCode) => Error.Validation(
        "INVALID_LANGUAGE_CODE",
        $"Language code '{languageCode}' is not valid. Expected format: 'xx-XX' (e.g., 'en-US', 'fr-FR')");

    public static Error LanguageAlreadySupported(string languageCode) => Error.Validation(
        "LANGUAGE_ALREADY_SUPPORTED",
        $"Language '{languageCode}' is already in the supported languages list");

    public static Error CannotRemoveDefaultLanguage(string languageCode) => Error.Validation(
        "CANNOT_REMOVE_DEFAULT_LANGUAGE",
        $"Cannot remove default language '{languageCode}'. Change the default language first.");

    public static Error InvalidColorFormat(string color) => Error.Validation(
        "INVALID_COLOR_FORMAT",
        $"Color '{color}' is not a valid hex color format. Expected: #RRGGBB or #RGB");

    public static Error InvalidUrl(string url) => Error.Validation(
        "INVALID_URL",
        $"URL '{url}' is not valid");
}
