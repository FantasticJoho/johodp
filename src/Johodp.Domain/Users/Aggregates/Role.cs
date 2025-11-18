namespace Johodp.Domain.Users.Aggregates;

using Common;
using ValueObjects;

public class Role : AggregateRoot
{
    public RoleId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool RequiresMFA { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<PermissionId> _permissionIds = new();
    public IReadOnlyList<PermissionId> PermissionIds => _permissionIds.AsReadOnly();

    private Role() { }

    public static Role Create(string name, string description, bool requiresMFA = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Role description cannot be empty", nameof(description));

        return new Role
        {
            Id = RoleId.Create(),
            Name = name,
            Description = description,
            RequiresMFA = requiresMFA,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddPermission(PermissionId permissionId)
    {
        if (permissionId == null)
            throw new ArgumentNullException(nameof(permissionId));

        if (_permissionIds.Contains(permissionId))
            return; // Idempotent

        _permissionIds.Add(permissionId);
    }

    public void RemovePermission(PermissionId permissionId)
    {
        if (permissionId == null)
            throw new ArgumentNullException(nameof(permissionId));

        _permissionIds.Remove(permissionId);
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
