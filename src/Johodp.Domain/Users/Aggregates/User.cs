namespace Johodp.Domain.Users.Aggregates;

using Common;
using Events;
using ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;
using System.Linq;

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
    /// <summary>
    /// Gets a value indicating whether the user's email address has been confirmed.
    /// This property is used by ASP.NET Identity for authentication and account verification workflows.
    /// </summary>
    public bool EmailConfirmed { get; private set; }
    public bool MFAEnabled { get; private set; }
    public UserStatus Status { get; private set; } = UserStatus.PendingActivation;
    public DateTime? ActivatedAt { get; private set; }
    public string? PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Tenant membership is represented by the UserTenants association (many-to-many via join entity)
    public ICollection<Entities.UserTenant> UserTenants { get; set; } = new List<Entities.UserTenant>();

    /// <summary>
    /// Returns the primary TenantId for this user based on the UserTenants association.
    /// This should be used as the source of truth for tenant membership.
    /// </summary>
    // NOTE: Tenant membership is represented by the UserTenants association.
    // Do not expose a single "PrimaryTenantId" — a user can belong to multiple tenants.

    /// <summary>
    /// Indique si l'utilisateur est actif (statut Active)
    /// </summary>
    public bool IsActive => Status == UserStatus.Active;

    private User() { }

    /// <summary>
    /// Factory method to create a new User.
    /// </summary>
    /// <param name="email">User's email address (will be normalized to lowercase)</param>
    /// <param name="firstName">User's first name (max 50 characters)</param>
    /// <param name="lastName">User's last name (max 50 characters)</param>
    /// <param name="userTenants">Initial tenant associations as (tenantId, role) tuples. Can be empty or null.</param>
    /// <param name="createAsPending">If true, user starts in PendingActivation status and triggers activation email</param>
    /// <returns>New User instance</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static User Create(string email, string firstName, string lastName, IEnumerable<(TenantId tenantId, string role)>? userTenants = null, bool createAsPending = false)
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
            EmailConfirmed = false,
            Status = createAsPending ? UserStatus.PendingActivation : UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Add initial tenant associations if provided
        if (userTenants != null)
        {
            foreach (var (tenantId, role) in userTenants)
            {
                if (tenantId == null)
                    throw new ArgumentException("Tenant ID cannot be null", nameof(userTenants));
                
                if (string.IsNullOrWhiteSpace(role))
                    throw new ArgumentException("Role cannot be empty", nameof(userTenants));
                
                user.UserTenants.Add(new Entities.UserTenant
                {
                    UserId = user.Id,
                    TenantId = tenantId,
                    Role = role,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        // Fire domain event - use first tenant for event context if available
        var firstTenant = userTenants?.FirstOrDefault().tenantId;
        if (createAsPending)
        {
            user.AddDomainEvent(new UserPendingActivationEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                firstTenant?.Value
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

        // The tenant association is stored in the UserTenant association table.
        // Check the in-memory collection for an association to the requested tenant.
        if (UserTenants == null || !UserTenants.Any())
            return false;

        return UserTenants.Any(ut => ut.TenantId != null && ut.TenantId.Value == tenantId.Value);
    }

    /// <summary>
    /// Updates the user's role and authorization scope.
    /// </summary>
    /// <param name="role">New role (cannot be empty)</param>
    /// <param name="scope">New scope (cannot be empty)</param>
    /// <exception cref="ArgumentException">Thrown when role or scope is empty</exception>
    /// <summary>
    /// DEPRECATED: Roles are now managed per-tenant via UserTenant entity.
    /// Use AddTenantId()/RemoveTenantId() to manage tenant membership instead.
    /// This method is kept for backward compatibility but does nothing.
    /// </summary>
    [Obsolete("Roles are managed per-tenant via UserTenant entity. Use AddTenantId()/RemoveTenantId() instead.", false)]
    public void UpdateRoleAndScope(string role, string scope)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        // Role and scope are now managed in UserTenant entity, no longer here
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

    /// <summary>
    /// Adds the user to a new tenant with the specified role.
    /// Used for multi-tenant scenarios where a user is added to additional tenants after creation.
    /// </summary>
    /// <param name="tenantId">The tenant to add the user to</param>
    /// <param name="role">The user's role in this tenant (default: "User")</param>
    /// <exception cref="ArgumentNullException">Thrown if tenantId is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if user is already in this tenant</exception>
    public void AddTenantId(TenantId tenantId, string role = "User")
    {
        if (tenantId == null)
            throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null");
        
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));
        
        // Check if user already belongs to this tenant
        if (UserTenants.Any(ut => ut.TenantId != null && ut.TenantId.Value == tenantId.Value))
            throw new InvalidOperationException($"User {Email.Value} is already associated with tenant {tenantId.Value}");
        
        var userTenant = new Entities.UserTenant
        {
            UserId = Id,
            TenantId = tenantId,
            Role = role,
            AssignedAt = DateTime.UtcNow
        };
        
        UserTenants.Add(userTenant);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes the user from a tenant.
    /// Used to revoke access to a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant to remove the user from</param>
    public void RemoveTenantId(TenantId tenantId)
    {
        if (tenantId == null)
            return;
        
        var userTenant = UserTenants.FirstOrDefault(ut => ut.TenantId != null && ut.TenantId.Value == tenantId.Value);
        if (userTenant != null)
        {
            UserTenants.Remove(userTenant);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Determines if this user requires multi-factor authentication.
    /// Used during login to determine if MFA verification is needed.
    /// </summary>
    /// <returns>True if MFA is enabled for this user, false otherwise</returns>
    public bool RequiresMFA() => MFAEnabled;
}
