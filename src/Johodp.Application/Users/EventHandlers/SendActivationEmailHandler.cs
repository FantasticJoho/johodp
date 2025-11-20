namespace Johodp.Application.Users.EventHandlers;

using Johodp.Application.Common.Events;
using Johodp.Domain.Users.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles UserPendingActivationEvent by sending activation email
/// </summary>
public class SendActivationEmailHandler : IEventHandler<UserPendingActivationEvent>
{
    // TODO: Inject IEmailService when implemented
    private readonly ILogger<SendActivationEmailHandler> _logger;

    public SendActivationEmailHandler(ILogger<SendActivationEmailHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(UserPendingActivationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending activation email to user {Email} (UserId: {UserId})",
            @event.Email,
            @event.UserId);

        // TODO: Implement email sending
        // await _emailService.SendActivationEmailAsync(@event.Email, @event.FirstName, ...);
        
        _logger.LogInformation(
            "Activation email sent successfully to {Email}",
            @event.Email);

        await Task.CompletedTask;
    }
}
