namespace Johodp.Domain.Users.Aggregates;

using Common;
using ValueObjects;

public class Permission : AggregateRoot
{
    public PermissionId Id { get; private set; } = null!;
    public PermissionName Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Permission() { }

    public static Permission Create(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Permission name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Permission description cannot be empty", nameof(description));

        return new Permission
        {
            Id = PermissionId.Create(),
            Name = PermissionName.Create(name),
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
