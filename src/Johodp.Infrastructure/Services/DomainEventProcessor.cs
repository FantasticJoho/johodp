namespace Johodp.Infrastructure.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Johodp.Domain.Common;
using Johodp.Application.Common.Events;

/// <summary>
/// Background service that processes domain events from the channel
/// Resolves handlers dynamically and invokes them
/// </summary>
public class DomainEventProcessor : BackgroundService
{
    private readonly ChannelEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainEventProcessor> _logger;

    public DomainEventProcessor(
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILogger<DomainEventProcessor> logger)
    {
        _eventBus = (ChannelEventBus)eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Domain Event Processor started");

        await foreach (var domainEvent in _eventBus.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessEventAsync(domainEvent, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing domain event: {EventType} (ID: {EventId})",
                    domainEvent.GetType().Name,
                    domainEvent.Id);
            }
        }

        _logger.LogInformation("Domain Event Processor stopped");
    }

    private async Task ProcessEventAsync(DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        
        // Get all handlers for this event type (can have multiple)
        var handlers = scope.ServiceProvider.GetServices(handlerType);
        
        if (!handlers.Any())
        {
            _logger.LogDebug(
                "No handlers registered for event: {EventType}",
                eventType.Name);
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                var handleMethod = handlerType.GetMethod(nameof(IEventHandler<DomainEvent>.HandleAsync));
                var task = (Task)handleMethod!.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
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
                
                // Continue with other handlers even if one fails
            }
        }
    }
}
