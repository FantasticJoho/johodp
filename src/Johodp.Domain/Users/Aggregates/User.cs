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

    // Single tenant per user with role and scope
    public TenantId TenantId { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public string Scope { get; private set; } = null!;

    private User() { }

    public static User Create(string email, string firstName, string lastName, TenantId tenantId, string role = "user", string scope = "default", bool createAsPending = false)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name cannot be empty", nameof(firstName));
        
        if (firstName.Length > 50)
            throw new ArgumentException("First name cannot exceed 50 characters", nameof(firstName));
        
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name cannot be empty", nameof(lastName));
        
        if (lastName.Length > 50)
            throw new ArgumentException("Last name cannot exceed 50 characters", nameof(lastName));

        if (tenantId == null)
            throw new ArgumentNullException(nameof(tenantId), "TenantId is required");

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope cannot be empty", nameof(scope));

        var user = new User
        {
            Id = UserId.Create(),
            Email = Email.Create(email),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            TenantId = tenantId,
            Role = role,
            Scope = scope,
            EmailConfirmed = createAsPending ? false : false,
            Status = createAsPending ? UserStatus.PendingActivation : UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        if (createAsPending)
        {
            user.AddDomainEvent(new UserPendingActivationEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                tenantId.Value
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

    public bool BelongsToTenant(TenantId tenantId)
    {
        if (tenantId == null)
            return false;

        return TenantId.Value == tenantId.Value;
    }

    public void UpdateRoleAndScope(string role, string scope)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope cannot be empty", nameof(scope));

        Role = role;
        Scope = scope;
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
