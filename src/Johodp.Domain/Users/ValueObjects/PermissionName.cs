namespace Johodp.Domain.Users.ValueObjects;

using Common;

public class PermissionName : ValueObject
{
    public string Value { get; private set; } = default!;

    private PermissionName() { }

    private PermissionName(string value)
    {
        Value = value;
    }

    public static PermissionName Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Permission name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Permission name cannot exceed 100 characters", nameof(name));

        return new PermissionName(name.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
