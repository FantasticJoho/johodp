namespace Johodp.Messaging.Validation;

/// <summary>
/// Interface for request validators
/// </summary>
public interface IValidator<in TRequest>
{
    /// <summary>
    /// Validates the request and returns validation errors if any
    /// </summary>
    /// <param name="request">Request to validate</param>
    /// <returns>Dictionary of field errors (empty if valid)</returns>
    Task<IDictionary<string, string[]>> ValidateAsync(TRequest request);
}
