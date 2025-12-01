namespace Johodp.Domain.CustomConfigurations.ValueObjects;

using Johodp.Domain.Common;

/// <summary>
/// Value object representing a CustomConfiguration identifier
/// </summary>
public class CustomConfigurationId : ValueObject
{
    public Guid Value { get; private set; }

    private CustomConfigurationId() { }

    private CustomConfigurationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CustomConfiguration ID cannot be empty", nameof(value));

        Value = value;
    }

    public static CustomConfigurationId Create()
    {
        return new CustomConfigurationId(Guid.NewGuid());
    }

    public static CustomConfigurationId From(Guid value)
    {
        return new CustomConfigurationId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
