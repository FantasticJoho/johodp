namespace Johodp.Domain.Users.Events;

using Common;

public class UserEmailConfirmedEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }

    public UserEmailConfirmedEvent(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
    }
}
