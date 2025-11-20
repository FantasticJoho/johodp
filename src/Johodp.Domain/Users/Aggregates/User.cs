namespace Johodp.Domain.Users.Aggregates;

using Common;
using Events;
using ValueObjects;

public class UserStatus : Enumeration
{
    public static readonly UserStatus PendingActivation = new(0, nameof(PendingActivation));
    public static readonly UserStatus Active = new(1, nameof(Active));
    public static readonly UserStatus Suspended = new(2, nameof(Suspended));
    public static readonly UserStatus Deleted = new(3, nameof(Deleted));

    private UserStatus(int value, string name) : base(value, name) { }

    // Behavior methods
    public bool CanActivate() => this == PendingActivation;
    public bool CanLogin() => this == Active;
    public bool CanSuspend() => this == Active;
    public bool IsDeleted() => this == Deleted;
}

public class User : AggregateRoot
{
    public UserId Id { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public bool EmailConfirmed { get; private set; }
    public bool IsActive => Status == UserStatus.Active;
    public bool MFAEnabled { get; private set; }
    public UserStatus Status { get; private set; } = UserStatus.PendingActivation;
    public DateTime? ActivatedAt { get; private set; }
    public string? PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Multi-tenancy - supports multiple tenants
    private readonly List<string> _tenantIds = new();
    public IReadOnlyList<string> TenantIds => _tenantIds.AsReadOnly();

    // Relations
    public ScopeId? ScopeId { get; private set; }
    public Scope? Scope { get; private set; }

    private readonly List<Role> _roles = new();
    public IReadOnlyList<Role> Roles => _roles.AsReadOnly();

    private readonly List<Permission> _permissions = new();
    public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();


    private User() { }

    public static User Create(string email, string firstName, string lastName, string? tenantId = null, bool createAsPending = false)
    {
        var user = new User
        {
            Id = UserId.Create(),
            Email = Email.Create(email),
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = createAsPending ? false : false,
            Status = createAsPending ? UserStatus.PendingActivation : UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Add tenant if provided
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            user._tenantIds.Add(tenantId);
        }

        if (createAsPending)
        {
            user.AddDomainEvent(new UserPendingActivationEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                tenantId
            ));
        }
        else
        {
            user.AddDomainEvent(new UserRegisteredEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName
            ));
        }

        return user;
    }

    public void AddTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        if (_tenantIds.Contains(tenantId))
            return;

        _tenantIds.Add(tenantId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveTenant(string tenantId)
    {
        _tenantIds.Remove(tenantId);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool BelongsToTenant(string tenantId)
    {
        return _tenantIds.Contains(tenantId);
    }

    public void ConfirmEmail()
    {
        if (EmailConfirmed)
            throw new InvalidOperationException("Email is already confirmed");

        EmailConfirmed = true;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new UserEmailConfirmedEvent(Id.Value, Email.Value));
    }

    public void Activate()
    {
        if (Status != UserStatus.PendingActivation)
            throw new InvalidOperationException($"Cannot activate user with status {Status}");

        if (string.IsNullOrEmpty(PasswordHash))
            throw new InvalidOperationException("Cannot activate user without password");

        Status = UserStatus.Active;
        EmailConfirmed = true;
        ActivatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new UserActivatedEvent(Id.Value, Email.Value));
    }

    public void Suspend(string reason)
    {
        if (Status == UserStatus.Deleted)
            throw new InvalidOperationException("Cannot suspend a deleted user");

        Status = UserStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = UserStatus.Deleted;
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
