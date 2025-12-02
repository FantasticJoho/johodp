namespace Johodp.Application.Common.Results;

/// <summary>
/// Represents the type of error that occurred
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Error caused by invalid input or business rule violation
    /// </summary>
    Validation,
    
    /// <summary>
    /// Requested resource was not found
    /// </summary>
    NotFound,
    
    /// <summary>
    /// Operation conflicts with current state (e.g., duplicate, concurrent modification)
    /// </summary>
    Conflict,
    
    /// <summary>
    /// User lacks permission to perform the operation
    /// </summary>
    Forbidden,
    
    /// <summary>
    /// User is not authenticated
    /// </summary>
    Unauthorized,
    
    /// <summary>
    /// Unexpected system error
    /// </summary>
    Failure
}

/// <summary>
/// Represents an error with a code, message, and type
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Error code (e.g., "TENANT_NOT_FOUND", "INVALID_EMAIL")
    /// </summary>
    public string Code { get; init; }
    
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; init; }
    
    /// <summary>
    /// Type of error
    /// </summary>
    public ErrorType Type { get; init; }
    
    /// <summary>
    /// Optional additional metadata about the error
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    private Error(string code, string message, ErrorType type, Dictionary<string, object>? metadata = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a validation error
    /// </summary>
    public static Error Validation(string code, string message, Dictionary<string, object>? metadata = null)
        => new(code, message, ErrorType.Validation, metadata);

    /// <summary>
    /// Creates a not found error
    /// </summary>
    public static Error NotFound(string code, string message, Dictionary<string, object>? metadata = null)
        => new(code, message, ErrorType.NotFound, metadata);

    /// <summary>
    /// Creates a conflict error
    /// </summary>
    public static Error Conflict(string code, string message, Dictionary<string, object>? metadata = null)
        => new(code, message, ErrorType.Conflict, metadata);

    /// <summary>
    /// Creates a forbidden error
    /// </summary>
    public static Error Forbidden(string code, string message, Dictionary<string, object>? metadata = null)
        => new(code, message, ErrorType.Forbidden, metadata);

    /// <summary>
    /// Creates an unauthorized error
    /// </summary>
    public static Error Unauthorized(string code, string message, Dictionary<string, object>? metadata = null)
        => new(code, message, ErrorType.Unauthorized, metadata);

    /// <summary>
    /// Creates a failure error
    /// </summary>
    public static Error Failure(string code, string message, Dictionary<string, object>? metadata = null)
        => new(code, message, ErrorType.Failure, metadata);

    /// <summary>
    /// None error (represents no error)
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
}
