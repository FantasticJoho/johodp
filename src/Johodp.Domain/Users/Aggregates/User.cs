namespace Johodp.Domain.Users.Aggregates;

using Common;
using Events;
using ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

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

    // Multi-tenancy - supports multiple tenants with role and scope per tenant
    private readonly List<UserTenant> _userTenants = new();
    public IReadOnlyList<UserTenant> UserTenants => _userTenants.AsReadOnly();
    
    // Convenience property to get tenant IDs
    public IReadOnlyList<TenantId> TenantIds => _userTenants.Select(ut => ut.TenantId).ToList().AsReadOnly();


    private User() { }

    public static User Create(string email, string firstName, string lastName, TenantId? tenantId = null, bool createAsPending = false)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name cannot be empty", nameof(firstName));
        
        if (firstName.Length > 50)
            throw new ArgumentException("First name cannot exceed 50 characters", nameof(firstName));
        
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name cannot be empty", nameof(lastName));
        
        if (lastName.Length > 50)
            throw new ArgumentException("Last name cannot exceed 50 characters", nameof(lastName));

        var user = new User
        {
            Id = UserId.Create(),
            Email = Email.Create(email),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            EmailConfirmed = createAsPending ? false : false,
            Status = createAsPending ? UserStatus.PendingActivation : UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Note: Tenant will be added via AddTenant method with role and scope
        // tenantId parameter kept for backward compatibility in events

        if (createAsPending)
        {
            user.AddDomainEvent(new UserPendingActivationEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                tenantId?.Value
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

    public void AddTenant(TenantId tenantId, string role, string scope)
    {
        if (tenantId == null)
            throw new ArgumentNullException(nameof(tenantId));

        if (_userTenants.Any(ut => ut.TenantId.Value == tenantId.Value))
            throw new InvalidOperationException($"User already belongs to tenant {tenantId.Value}");

        var userTenant = UserTenant.Create(Id, tenantId, role, scope);
        _userTenants.Add(userTenant);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveTenant(TenantId tenantId)
    {
        if (tenantId == null)
            throw new ArgumentNullException(nameof(tenantId));

        var existing = _userTenants.FirstOrDefault(ut => ut.TenantId.Value == tenantId.Value);
        if (existing != null)
        {
            _userTenants.Remove(existing);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public bool BelongsToTenant(TenantId tenantId)
    {
        if (tenantId == null)
            return false;

        return _userTenants.Any(ut => ut.TenantId.Value == tenantId.Value);
    }

    public UserTenant? GetTenantContext(TenantId tenantId)
    {
        if (tenantId == null)
            return null;

        return _userTenants.FirstOrDefault(ut => ut.TenantId.Value == tenantId.Value);
    }

    public void UpdateTenantRoleAndScope(TenantId tenantId, string role, string scope)
    {
        if (tenantId == null)
            throw new ArgumentNullException(nameof(tenantId));

        var userTenant = _userTenants.FirstOrDefault(ut => ut.TenantId.Value == tenantId.Value);
        if (userTenant == null)
            throw new InvalidOperationException($"User does not belong to tenant {tenantId.Value}");

        userTenant.Update(role, scope);
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
}
