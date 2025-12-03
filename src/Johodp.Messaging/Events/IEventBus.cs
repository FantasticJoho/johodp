namespace Johodp.Messaging.Events;

/// <summary>
/// Event bus for publishing domain events asynchronously
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered handlers (non-blocking)
    /// </summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent;
}
