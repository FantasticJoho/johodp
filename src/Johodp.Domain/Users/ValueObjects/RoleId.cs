namespace Johodp.Domain.Users.ValueObjects;

using Common;

public class RoleId : ValueObject
{
    public Guid Value { get; private set; }

    private RoleId() { }

    private RoleId(Guid value)
    {
        Value = value;
    }

    public static RoleId Create() => new(Guid.NewGuid());

    public static RoleId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Role ID cannot be empty", nameof(value));

        return new RoleId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
