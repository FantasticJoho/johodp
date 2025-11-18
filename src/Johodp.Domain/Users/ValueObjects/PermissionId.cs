namespace Johodp.Domain.Users.ValueObjects;

using Common;

public class PermissionId : ValueObject
{
    public Guid Value { get; private set; }

    private PermissionId() { }

    private PermissionId(Guid value)
    {
        Value = value;
    }

    public static PermissionId Create() => new(Guid.NewGuid());

    public static PermissionId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Permission ID cannot be empty", nameof(value));

        return new PermissionId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
