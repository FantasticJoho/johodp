namespace Johodp.Infrastructure.Services;

using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Common;
using MediatR;

public class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IMediator _mediator;

    public DomainEventPublisher(IMediator mediator)
    {
        _mediator = mediator;
    }
    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var notification = new DomainEventNotification(domainEvent);
        await _mediator.Publish(notification, cancellationToken);
    }

    public async Task PublishAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notification = new DomainEventNotification(domainEvent);
            await _mediator.Publish(notification, cancellationToken);
        }
    }
}

internal class DomainEventNotification : INotification
{
    public DomainEvent DomainEvent { get; }

    public DomainEventNotification(DomainEvent domainEvent)
    {
        DomainEvent = domainEvent;
    }
}
