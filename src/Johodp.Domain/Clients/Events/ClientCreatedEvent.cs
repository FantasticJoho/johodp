namespace Johodp.Domain.Clients.Events;

using Common;

public class ClientCreatedEvent : DomainEvent
{
    public Guid ClientId { get; set; }
    public string ClientName { get; set; }
    public string[] AllowedRedirectUris { get; set; }

    public ClientCreatedEvent(Guid clientId, string clientName, string[] allowedRedirectUris)
    {
        ClientId = clientId;
        ClientName = clientName;
        AllowedRedirectUris = allowedRedirectUris;
    }
}
