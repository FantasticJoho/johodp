namespace Johodp.Domain.Users.ValueObjects;

using Common;

public class UserId : ValueObject
{
    public Guid Value { get; private set; }

    private UserId() { }

    private UserId(Guid value)
    {
        Value = value;
    }

    public static UserId Create() => new(Guid.NewGuid());

    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(value));

        return new UserId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
