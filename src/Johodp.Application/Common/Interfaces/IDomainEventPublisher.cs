namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Common;

public interface IDomainEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task PublishAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
