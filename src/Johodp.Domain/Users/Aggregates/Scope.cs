namespace Johodp.Domain.Users.Aggregates;

using Common;
using ValueObjects;

public class Scope : AggregateRoot
{
    public ScopeId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Scope() { }

    public static Scope Create(string name, string code, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Scope name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Scope code cannot be empty", nameof(code));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Scope description cannot be empty", nameof(description));

        return new Scope
        {
            Id = ScopeId.Create(),
            Name = name,
            Code = code.ToUpperInvariant(),
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
