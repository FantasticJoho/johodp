namespace Johodp.Application.Common.Interfaces;

/// <summary>
/// Service de gestion de l'activation des comptes utilisateurs
/// </summary>
public interface IUserActivationService
{
    /// <summary>
    /// Génère un token d'activation et envoie l'email d'activation à l'utilisateur
    /// </summary>
    /// <param name="userId">ID de l'utilisateur</param>
    /// <param name="email">Adresse email de l'utilisateur</param>
    /// <param name="firstName">Prénom de l'utilisateur</param>
    /// <param name="lastName">Nom de l'utilisateur</param>
    /// <param name="tenantId">ID du tenant (optionnel)</param>
    /// <returns>True si l'email a été envoyé avec succès</returns>
    Task<bool> SendActivationEmailAsync(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        Guid? tenantId = null);

    /// <summary>
    /// Active un compte utilisateur avec le token fourni
    /// </summary>
    /// <param name="userId">ID de l'utilisateur</param>
    /// <param name="activationToken">Token d'activation</param>
    /// <param name="newPassword">Nouveau mot de passe</param>
    /// <returns>True si l'activation a réussi</returns>
    Task<bool> ActivateUserAsync(
        Guid userId,
        string activationToken,
        string newPassword);
}
