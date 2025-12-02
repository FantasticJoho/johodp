namespace Johodp.Application.Clients;

using Johodp.Application.Common.Results;

/// <summary>
/// Centralized error messages for Client operations
/// </summary>
public static class ClientErrors
{
    // Conflict errors
    public static Error AlreadyExists(string clientName) => Error.Conflict(
        "CLIENT_ALREADY_EXISTS",
        $"A client with name '{clientName}' already exists");

    // NotFound errors
    public static Error NotFound(Guid clientId) => Error.NotFound(
        "CLIENT_NOT_FOUND",
        $"Client with ID '{clientId}' not found");

    public static Error NotFoundByName(string clientName) => Error.NotFound(
        "CLIENT_NOT_FOUND",
        $"Client with name '{clientName}' not found");

    // Validation errors
    public static Error InvalidClientName() => Error.Validation(
        "INVALID_CLIENT_NAME",
        "Client name cannot be empty or whitespace");

    public static Error ScopesRequired() => Error.Validation(
        "SCOPES_REQUIRED",
        "At least one scope must be specified for the client");
}
