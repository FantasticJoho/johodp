namespace Johodp.Domain.Users.Aggregates;

using Common;
using Events;
using ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

/// <summary>
/// Represents the lifecycle status of a user account.
/// Users progress through states: PendingActivation → Active → Suspended/Deleted.
/// </summary>
public class UserStatus : Enumeration
{
    /// <summary>Initial state after registration, waiting for email confirmation and password setup.</summary>
    public static readonly UserStatus PendingActivation = new(0, nameof(PendingActivation));
    
    /// <summary>Active user who can authenticate and use the system.</summary>
    public static readonly UserStatus Active = new(1, nameof(Active));
    
    /// <summary>Temporarily disabled user (can be reactivated).</summary>
    public static readonly UserStatus Suspended = new(2, nameof(Suspended));
    
    /// <summary>Permanently deleted user (soft delete).</summary>
    public static readonly UserStatus Deleted = new(3, nameof(Deleted));

    private UserStatus(int value, string name) : base(value, name) { }

    /// <summary>Determines if user can transition to Active status.</summary>
    public bool CanActivate() => this == PendingActivation;
    
    /// <summary>Determines if user can authenticate and access the system.</summary>
    public bool CanLogin() => this == Active;
    
    /// <summary>Determines if user can be suspended.</summary>
    public bool CanSuspend() => this == Active;
    
    /// <summary>Determines if user has been permanently deleted.</summary>
    public bool IsDeleted() => this == Deleted;
}

/// <summary>
/// User aggregate root - represents a user account in the multi-tenant system.
/// Each user belongs to exactly one tenant and has a single role/scope combination.
/// </summary>
/// <remarks>
/// <para><strong>Business Rules:</strong></para>
/// <list type="bullet">
/// <item>Email must be unique per tenant (composite key: Email + TenantId)</item>
/// <item>Users start in PendingActivation status after registration</item>
/// <item>Password hash is set during activation (not before)</item>
/// <item>MFA can only be enabled after activation</item>
/// <item>Email confirmation happens during activation</item>
/// <item>FirstName and LastName limited to 50 characters each</item>
/// </list>
/// 
/// <para><strong>Lifecycle:</strong></para>
/// <code>
/// PendingActivation → Activate() → Active → Suspend() → Suspended
///                                        ↓
///                                  Deactivate() → Deleted
/// </code>
/// 
/// <para><strong>Domain Events:</strong></para>
/// <list type="bullet">
/// <item>UserPendingActivationEvent - fired when user is created as pending</item>
/// <item>UserRegisteredEvent - fired when user is created as active</item>
/// <item>UserEmailConfirmedEvent - fired when email is confirmed</item>
/// <item>UserActivatedEvent - fired when user completes activation</item>
/// </list>
/// </remarks>
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

    /// <summary>
    /// Factory method to create a new User.
    /// </summary>
    /// <param name="email">User's email address (will be normalized to lowercase)</param>
    /// <param name="firstName">User's first name (max 50 characters)</param>
    /// <param name="lastName">User's last name (max 50 characters)</param>
    /// <param name="tenantId">Tenant the user belongs to (required)</param>
    /// <param name="role">User's role in the system (default: "user")</param>
    /// <param name="scope">User's authorization scope (default: "default")</param>
    /// <param name="createAsPending">If true, user starts in PendingActivation status and triggers activation email</param>
    /// <returns>New User instance</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    /// <exception cref="ArgumentNullException">Thrown when tenantId is null</exception>
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

    /// <summary>
    /// Checks if this user belongs to the specified tenant.
    /// Used for multi-tenant authorization checks.
    /// </summary>
    /// <param name="tenantId">Tenant ID to check</param>
    /// <returns>True if user belongs to the tenant, false otherwise</returns>
    public bool BelongsToTenant(TenantId tenantId)
    {
        if (tenantId == null)
            return false;

        return TenantId.Value == tenantId.Value;
    }

    /// <summary>
    /// Updates the user's role and authorization scope.
    /// </summary>
    /// <param name="role">New role (cannot be empty)</param>
    /// <param name="scope">New scope (cannot be empty)</param>
    /// <exception cref="ArgumentException">Thrown when role or scope is empty</exception>
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

    /// <summary>
    /// Confirms the user's email address.
    /// Fires UserEmailConfirmedEvent domain event.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if email is already confirmed</exception>
    public void ConfirmEmail()
    {
        if (EmailConfirmed)
            throw new InvalidOperationException("Email is already confirmed");

        EmailConfirmed = true;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new UserEmailConfirmedEvent(Id.Value, Email.Value));
    }

    /// <summary>
    /// Activates a pending user account.
    /// Transitions user from PendingActivation to Active status.
    /// Requires password to be set beforehand.
    /// Fires UserActivatedEvent domain event.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if user is not in PendingActivation status or password is not set</exception>
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

    /// <summary>
    /// Temporarily suspends the user account.
    /// User can be reactivated later.
    /// </summary>
    /// <param name="reason">Reason for suspension (for audit purposes)</param>
    /// <exception cref="InvalidOperationException">Thrown if user is already deleted</exception>
    public void Suspend(string reason)
    {
        if (Status == UserStatus.Deleted)
            throw new InvalidOperationException("Cannot suspend a deleted user");

        Status = UserStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Permanently deactivates (soft delete) the user account.
    /// This operation is irreversible.
    /// </summary>
    public void Deactivate()
    {
        Status = UserStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Enables multi-factor authentication for this user.
    /// Should be called after TOTP enrollment is verified.
    /// </summary>
    public void EnableMFA()
    {
        MFAEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Disables multi-factor authentication for this user.
    /// </summary>
    public void DisableMFA()
    {
        MFAEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the password hash for this user.
    /// Used during activation and password reset operations.
    /// </summary>
    /// <param name="hash">BCrypt or similar password hash (nullable for accounts without password)</param>
    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
        UpdatedAt = DateTime.UtcNow;
    }
}
