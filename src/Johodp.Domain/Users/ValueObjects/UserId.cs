namespace Johodp.Domain.Users.ValueObjects;

using Common;

/// <summary>
/// UserId value object - represents a unique user identifier.
/// Wraps a GUID to provide type safety and prevent primitive obsession.
/// </summary>
/// <remarks>
/// <para><strong>DDD Pattern:</strong> Value Object (Identity)</para>
/// <para>Provides strongly-typed identifier instead of using raw Guid.</para>
/// <para>Prevents accidentally mixing User IDs with other entity IDs (Tenant, Client, etc.).</para>
/// 
/// <para><strong>Usage:</strong></para>
/// <code>
/// var newId = UserId.Create();              // Generate new ID
/// var existingId = UserId.From(guid);       // Reconstruct from Guid
/// </code>
/// </remarks>
public class UserId : ValueObject
{
    public Guid Value { get; private set; }

    private UserId() { }

    private UserId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new UserId with a generated GUID.
    /// Use this when creating new users.
    /// </summary>
    /// <returns>New UserId with unique identifier</returns>
    public static UserId Create() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a UserId from an existing GUID value.
    /// Use this when reconstructing from database or external sources.
    /// </summary>
    /// <param name="value">Existing GUID value</param>
    /// <returns>UserId instance</returns>
    /// <exception cref="ArgumentException">Thrown when value is Guid.Empty</exception>
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
