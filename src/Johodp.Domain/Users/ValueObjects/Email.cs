namespace Johodp.Domain.Users.ValueObjects;

using Common;

public class Email : ValueObject
{
    public string Value { get; private set; } = default!;

    private Email() { }

    private Email(string value)
    {
        Value = value;
    }

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
