namespace Johodp.Infrastructure.Services;

using System.Threading.Channels;
using Johodp.Application.Common.Events;
using Johodp.Domain.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Channel-based event bus for async domain event processing
/// High performance, bounded capacity with backpressure
/// </summary>
public class ChannelEventBus : IEventBus
{
    private readonly Channel<DomainEvent> _channel;
    private readonly ILogger<ChannelEventBus> _logger;

    public ChannelEventBus(ILogger<ChannelEventBus> logger)
    {
        _logger = logger;
        
        // Bounded channel with capacity 1000 (adjust based on load)
        // Wait strategy: Wait for space if full (backpressure)
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        
        _channel = Channel.CreateBounded<DomainEvent>(options);
    }

    /// <summary>
    /// Channel reader for background processing
    /// </summary>
    public ChannelReader<DomainEvent> Reader => _channel.Reader;

    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent
    {
        try
        {
            await _channel.Writer.WriteAsync(@event, cancellationToken);
            
            _logger.LogDebug(
                "Domain event queued: {EventType} (ID: {EventId})", 
                @event.GetType().Name, 
                @event.Id);
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning(
                "Channel closed, cannot publish event: {EventType}", 
                @event.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Failed to publish event to channel: {EventType}", 
                @event.GetType().Name);
        }
    }
}
