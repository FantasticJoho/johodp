namespace Johodp.Messaging.Validation;

/// <summary>
/// Exception thrown when request validation fails
/// </summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, params string[] errors)
        : this(new Dictionary<string, string[]> { [field] = errors })
    {
    }
}
