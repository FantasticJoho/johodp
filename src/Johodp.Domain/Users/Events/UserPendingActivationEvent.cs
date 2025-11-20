namespace Johodp.Domain.Users.Events;

using Common;

public class UserPendingActivationEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? TenantId { get; set; }

    public UserPendingActivationEvent(Guid userId, string email, string firstName, string lastName, string? tenantId)
    {
        UserId = userId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        TenantId = tenantId;
    }
}
