namespace Johodp.Domain.Tenants.ValueObjects;

using Johodp.Domain.Common;

public class TenantId : ValueObject
{
    public Guid Value { get; private set; }

    private TenantId() { }

    private TenantId(Guid value)
    {
        Value = value;
    }

    public static TenantId Create() => new(Guid.NewGuid());

    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty", nameof(value));

        return new TenantId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
