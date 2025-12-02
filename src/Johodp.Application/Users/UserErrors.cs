namespace Johodp.Application.Users;

using Johodp.Application.Common.Results;

/// <summary>
/// Centralized error messages for User operations
/// </summary>
public static class UserErrors
{
    // Conflict errors
    public static Error AlreadyExists(string email, Guid tenantId) => Error.Conflict(
        "USER_ALREADY_EXISTS",
        $"User with email '{email}' already exists for this tenant");

    public static Error AlreadyExistsForTenant(string email) => Error.Conflict(
        "USER_ALREADY_EXISTS",
        $"User with email {email} already exists for this tenant");

    // NotFound errors
    public static Error NotFound(Guid userId) => Error.NotFound(
        "USER_NOT_FOUND",
        $"User with ID {userId} not found");

    public static Error NotFoundByEmail(string email) => Error.NotFound(
        "USER_NOT_FOUND",
        $"User with email '{email}' not found");

    // Validation errors
    public static Error InvalidEmail(string email) => Error.Validation(
        "INVALID_EMAIL",
        $"Email '{email}' is not valid");

    public static Error TenantRequired() => Error.Validation(
        "TENANT_REQUIRED",
        "TenantId is required. A user must belong to a tenant.");

    public static Error InvalidRole(string role) => Error.Validation(
        "INVALID_ROLE",
        $"Role '{role}' is not valid");

    // Authorization errors
    public static Error EmailNotConfirmed() => Error.Forbidden(
        "EMAIL_NOT_CONFIRMED",
        "Email address must be confirmed before this action");

    public static Error AccountDeactivated() => Error.Forbidden(
        "ACCOUNT_DEACTIVATED",
        "User account is deactivated");
}
