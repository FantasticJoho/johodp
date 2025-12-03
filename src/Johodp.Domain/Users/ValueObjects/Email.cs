namespace Johodp.Domain.Users.ValueObjects;

using Common;

/// <summary>
/// Email value object - represents a validated email address.
/// Immutable and ensures email validity through validation in Create factory method.
/// </summary>
/// <remarks>
/// <para><strong>Validation Rules:</strong></para>
/// <list type="bullet">
/// <item>Cannot be null or whitespace</item>
/// <item>Must contain '@' symbol (basic email format check)</item>
/// <item>Automatically normalized to lowercase for consistency</item>
/// </list>
/// 
/// <para><strong>DDD Pattern:</strong> Value Object</para>
/// <para>Email addresses are compared by value, not reference.</para>
/// <para>Two Email objects with the same address are considered equal.</para>
/// </remarks>
public class Email : ValueObject
{
    public string Value { get; private set; } = default!;

    private Email() { }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Email value object with validation.
    /// Email is automatically normalized to lowercase.
    /// </summary>
    /// <param name="email">Email address string</param>
    /// <returns>Validated Email value object</returns>
    /// <exception cref="ArgumentException">Thrown when email is empty or invalid format</exception>
    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        if (!email.Contains("@"))
            throw new ArgumentException("Email must be valid", nameof(email));

        return new Email(email.ToLowerInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
