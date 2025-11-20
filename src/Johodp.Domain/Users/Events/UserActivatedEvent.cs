namespace Johodp.Domain.Users.Events;

using Johodp.Domain.Common;

public class UserActivatedEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }

    public UserActivatedEvent(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
    }
}
