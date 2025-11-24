namespace Johodp.Application.Common.Interfaces;

/// <summary>
/// Service d'envoi d'emails pour les notifications utilisateur
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envoie un email d'activation de compte avec le token
    /// </summary>
    /// <param name="email">Adresse email du destinataire</param>
    /// <param name="firstName">Prénom de l'utilisateur</param>
    /// <param name="lastName">Nom de l'utilisateur</param>
    /// <param name="activationToken">Token d'activation à inclure dans le lien</param>
    /// <param name="userId">ID de l'utilisateur</param>
    /// <param name="tenantId">ID du tenant (optionnel)</param>
    /// <returns>True si l'email a été envoyé avec succès</returns>
    Task<bool> SendActivationEmailAsync(
        string email,
        string firstName,
        string lastName,
        string activationToken,
        Guid userId,
        string? tenantId = null);

    /// <summary>
    /// Envoie un email de réinitialisation de mot de passe
    /// </summary>
    /// <param name="email">Adresse email du destinataire</param>
    /// <param name="firstName">Prénom de l'utilisateur</param>
    /// <param name="resetToken">Token de réinitialisation</param>
    /// <param name="userId">ID de l'utilisateur</param>
    /// <returns>True si l'email a été envoyé avec succès</returns>
    Task<bool> SendPasswordResetEmailAsync(
        string email,
        string firstName,
        string resetToken,
        Guid userId);

    /// <summary>
    /// Envoie un email de bienvenue après activation du compte
    /// </summary>
    /// <param name="email">Adresse email du destinataire</param>
    /// <param name="firstName">Prénom de l'utilisateur</param>
    /// <param name="lastName">Nom de l'utilisateur</param>
    /// <param name="tenantName">Nom du tenant (optionnel)</param>
    /// <returns>True si l'email a été envoyé avec succès</returns>
    Task<bool> SendWelcomeEmailAsync(
        string email,
        string firstName,
        string lastName,
        string? tenantName = null);

    /// <summary>
    /// Envoie un email de notification générique
    /// </summary>
    /// <param name="email">Adresse email du destinataire</param>
    /// <param name="subject">Sujet de l'email</param>
    /// <param name="body">Corps de l'email (HTML supporté)</param>
    /// <returns>True si l'email a été envoyé avec succès</returns>
    Task<bool> SendEmailAsync(
        string email,
        string subject,
        string body);
}
