namespace Johodp.Domain.Users.Aggregates;

using Common;
using Events;
using ValueObjects;

public class User : AggregateRoot
{
    public UserId Id { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public bool EmailConfirmed { get; private set; }
    public bool IsActive { get; private set; }
    public bool MFAEnabled { get; private set; }
    public string? PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Multi-tenancy
    public string? TenantId { get; private set; }

    // Relations
    public ScopeId? ScopeId { get; private set; }
    public Scope? Scope { get; private set; }

    private readonly List<Role> _roles = new();
    public IReadOnlyList<Role> Roles => _roles.AsReadOnly();

    private readonly List<Permission> _permissions = new();
    public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();


    private User() { }

    public static User Create(string email, string firstName, string lastName, string? tenantId = null)
    {
        var user = new User
        {
            Id = UserId.Create(),
            Email = Email.Create(email),
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId
        };

        user.AddDomainEvent(new UserRegisteredEvent(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName
        ));

        return user;
    }

    public void SetTenantId(string? tenantId)
    {
        TenantId = tenantId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfirmEmail()
    {
        if (EmailConfirmed)
            throw new InvalidOperationException("Email is already confirmed");

        EmailConfirmed = true;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new UserEmailConfirmedEvent(Id.Value, Email.Value));
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetScope(Scope scope)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        Scope = scope;
        ScopeId = scope.Id;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddRole(Role role)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        if (_roles.Any(r => r.Id.Value == role.Id.Value))
            return; // Idempotent

        _roles.Add(role);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveRole(RoleId roleId)
    {
        if (roleId == null)
            throw new ArgumentNullException(nameof(roleId));

        var role = _roles.FirstOrDefault(r => r.Id.Value == roleId.Value);
        if (role != null)
        {
            _roles.Remove(role);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void AddPermission(Permission permission)
    {
        if (permission == null)
            throw new ArgumentNullException(nameof(permission));

        if (_permissions.Any(p => p.Id.Value == permission.Id.Value))
            return; // Idempotent

        _permissions.Add(permission);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemovePermission(PermissionId permissionId)
    {
        if (permissionId == null)
            throw new ArgumentNullException(nameof(permissionId));

        var permission = _permissions.FirstOrDefault(p => p.Id.Value == permissionId.Value);
        if (permission != null)
        {
            _permissions.Remove(permission);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void EnableMFA()
    {
        MFAEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableMFA()
    {
        MFAEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RequiresMFA()
    {
        return _roles.Any(r => r.RequiresMFA && r.IsActive);
    }
}
