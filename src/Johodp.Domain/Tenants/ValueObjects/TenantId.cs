namespace Johodp.Domain.Tenants.ValueObjects;

using Johodp.Domain.Common;

/// <summary>
/// TenantId value object - represents a unique tenant identifier.
/// Wraps a GUID to provide type safety and prevent primitive obsession.
/// </summary>
/// <remarks>
/// <para><strong>DDD Pattern:</strong> Value Object (Identity)</para>
/// <para>Provides strongly-typed identifier for multi-tenant architecture.</para>
/// <para>Ensures tenant IDs cannot be confused with user or client IDs at compile-time.</para>
/// 
/// <para><strong>Multi-Tenancy:</strong></para>
/// <para>Each tenant represents an isolated customer/organization in the system.</para>
/// <para>Users belong to exactly one tenant, ensuring data isolation.</para>
/// </remarks>
public class TenantId : ValueObject
{
    public Guid Value { get; private set; }

    private TenantId() { }

    private TenantId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new TenantId with a generated GUID.
    /// Use this when creating new tenants.
    /// </summary>
    /// <returns>New TenantId with unique identifier</returns>
    public static TenantId Create() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a TenantId from an existing GUID value.
    /// Use this when reconstructing from database or external sources.
    /// </summary>
    /// <param name="value">Existing GUID value</param>
    /// <returns>TenantId instance</returns>
    /// <exception cref="ArgumentException">Thrown when value is Guid.Empty</exception>
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
