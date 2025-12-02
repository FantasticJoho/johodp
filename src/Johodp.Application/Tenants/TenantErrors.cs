namespace Johodp.Application.Tenants;

using Johodp.Application.Common.Results;

/// <summary>
/// Centralized error messages for Tenant operations
/// </summary>
public static class TenantErrors
{
    // Conflict errors
    public static Error AlreadyExists(string tenantName) => Error.Conflict(
        "TENANT_ALREADY_EXISTS",
        $"A tenant with name '{tenantName}' already exists");

    // Validation errors
    public static Error ClientIdRequired() => Error.Validation(
        "CLIENT_ID_REQUIRED",
        "ClientId is required. A tenant must be associated with an existing client.");

    public static Error InvalidClientId(string clientId) => Error.Validation(
        "INVALID_CLIENT_ID",
        $"ClientId '{clientId}' is not a valid GUID.");

    public static Error CustomConfigRequired() => Error.Validation(
        "CUSTOM_CONFIG_REQUIRED",
        "CustomConfigurationId is required. A tenant must reference a CustomConfiguration.");

    public static Error EmptyCustomConfig() => Error.Validation(
        "EMPTY_CUSTOM_CONFIG",
        "CustomConfigurationId cannot be empty. A tenant must have a valid CustomConfiguration.");

    // NotFound errors
    public static Error NotFound(Guid tenantId) => Error.NotFound(
        "TENANT_NOT_FOUND",
        $"Tenant with ID '{tenantId}' not found");

    public static Error NotFoundByName(string tenantName) => Error.NotFound(
        "TENANT_NOT_FOUND",
        $"Tenant with name '{tenantName}' not found");

    public static Error ClientNotFound(string clientId) => Error.NotFound(
        "CLIENT_NOT_FOUND",
        $"Client '{clientId}' does not exist. Please create the client first.");

    public static Error CustomConfigNotFound(Guid configId) => Error.NotFound(
        "CUSTOM_CONFIG_NOT_FOUND",
        $"CustomConfiguration with ID '{configId}' not found");
}
