namespace Johodp.Messaging.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Simple event aggregator that synchronously invokes all registered handlers
/// Replaces complex Channel-based event bus with direct handler invocation
/// </summary>
public class EventAggregator : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventAggregator> _logger;

    public EventAggregator(
        IServiceProvider serviceProvider,
        ILogger<EventAggregator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);

        // Resolve all handlers for this event type
        var handlers = _serviceProvider.GetServices(handlerType);

        if (!handlers.Any())
        {
            _logger.LogDebug(
                "No handlers registered for event: {EventType} (ID: {EventId})",
                eventType.Name,
                @event.Id);
            return;
        }

        _logger.LogDebug(
            "Publishing event: {EventType} (ID: {EventId}) to {HandlerCount} handler(s)",
            eventType.Name,
            @event.Id,
            handlers.Count());

        // Invoke each handler sequentially
        foreach (var handler in handlers)
        {
            if (handler == null)
                continue;

            try
            {
                var handleMethod = handlerType.GetMethod(nameof(IEventHandler<DomainEvent>.HandleAsync));
                var task = (Task)handleMethod!.Invoke(handler, new object[] { @event, cancellationToken })!;
                await task;

                _logger.LogDebug(
                    "Event handled successfully: {EventType} by {HandlerType}",
                    eventType.Name,
                    handler.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Handler failed for event: {EventType}, Handler: {HandlerType}",
                    eventType.Name,
                    handler.GetType().Name);

                // Re-throw to propagate error to caller
                // Caller (CommandHandler) can decide to retry or fail the operation
                throw;
            }
        }
    }
}
