namespace Johodp.Application.Users.EventHandlers;

using Johodp.Messaging.Events;
using Johodp.Domain.Users.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles UserActivatedEvent for logging/analytics
/// </summary>
public class UserActivatedEventHandler : IEventHandler<UserActivatedEvent>
{
    private readonly ILogger<UserActivatedEventHandler> _logger;

    public UserActivatedEventHandler(ILogger<UserActivatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(UserActivatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "User activated: {Email} (UserId: {UserId})",
            @event.Email,
            @event.UserId);

        // TODO: Add analytics tracking, send welcome email, etc.
        
        await Task.CompletedTask;
    }
}
