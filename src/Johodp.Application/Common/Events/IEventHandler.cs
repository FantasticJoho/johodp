namespace Johodp.Application.Common.Events;

using Johodp.Domain.Common;

/// <summary>
/// Handler for domain events
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
