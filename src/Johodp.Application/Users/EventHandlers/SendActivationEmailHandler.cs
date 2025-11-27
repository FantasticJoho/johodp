namespace Johodp.Application.Users.EventHandlers;

using Johodp.Application.Common.Events;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles UserPendingActivationEvent by triggering activation email sending
/// </summary>
public class SendActivationEmailHandler : IEventHandler<UserPendingActivationEvent>
{
    private readonly IUserActivationService _userActivationService;
    private readonly ILogger<SendActivationEmailHandler> _logger;

    public SendActivationEmailHandler(
        IUserActivationService userActivationService,
        ILogger<SendActivationEmailHandler> logger)
    {
        _userActivationService = userActivationService;
        _logger = logger;
    }

    public async Task HandleAsync(UserPendingActivationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "User pending activation event received for {Email} (UserId: {UserId}, TenantId: {TenantId})",
            @event.Email,
            @event.UserId,
            @event.TenantId?.ToString() ?? "none");

        // Envoyer l'email d'activation via le service dédié
        // Le service génère le token et envoie l'email
        var sent = await _userActivationService.SendActivationEmailAsync(
            @event.UserId,
            @event.Email,
            @event.FirstName,
            @event.LastName,
            @event.TenantId);

        if (sent)
        {
            _logger.LogInformation(
                "Activation email triggered successfully for {Email} (UserId: {UserId})",
                @event.Email,
                @event.UserId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to trigger activation email for {Email} (UserId: {UserId})",
                @event.Email,
                @event.UserId);
        }
    }
}
