namespace Johodp.Domain.Clients.ValueObjects;

using Common;

public class ClientId : ValueObject
{
    public Guid Value { get; private set; }

    private ClientId() { }

    private ClientId(Guid value)
    {
        Value = value;
    }

    public static ClientId Create() => new(Guid.NewGuid());

    public static ClientId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Client ID cannot be empty", nameof(value));

        return new ClientId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public class ClientSecret : ValueObject
{
    public string Value { get; private set; } = default!;

    private ClientSecret() { }

    private ClientSecret(string value)
    {
        Value = value;
    }

    public static ClientSecret Create(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Client secret cannot be empty", nameof(secret));

        return new ClientSecret(secret);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
