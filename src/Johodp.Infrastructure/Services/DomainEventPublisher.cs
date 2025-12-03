namespace Johodp.Infrastructure.Services;

using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Events;

/// <summary>
/// Publishes domain events to the channel-based event bus
/// </summary>
public class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IEventBus _eventBus;

    public DomainEventPublisher(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
    
    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await _eventBus.PublishAsync(domainEvent, cancellationToken);
    }

    public async Task PublishAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _eventBus.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
