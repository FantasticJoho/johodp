namespace Johodp.Application.Common.Interfaces;

/// <summary>
/// Service pour notifier les applications tierces (fire-and-forget)
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notifie l'application tierce qu'une demande de création de compte a été reçue
    /// </summary>
    Task NotifyAccountRequestAsync(
        string tenantId,
        string email,
        string firstName,
        string lastName,
        string requestId);
}
