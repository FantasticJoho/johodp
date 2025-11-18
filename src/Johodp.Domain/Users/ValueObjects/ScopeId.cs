namespace Johodp.Domain.Users.ValueObjects;

using Common;

public class ScopeId : ValueObject
{
    public Guid Value { get; private set; }

    private ScopeId() { }

    private ScopeId(Guid value)
    {
        Value = value;
    }

    public static ScopeId Create() => new(Guid.NewGuid());

    public static ScopeId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Scope ID cannot be empty", nameof(value));

        return new ScopeId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
