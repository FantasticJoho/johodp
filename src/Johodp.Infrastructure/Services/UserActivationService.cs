namespace Johodp.Infrastructure.Services;

using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.Aggregates;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service de gestion de l'activation des comptes utilisateurs
/// Fait le pont entre la couche Application et l'infrastructure ASP.NET Identity
/// </summary>
public class UserActivationService : IUserActivationService
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserActivationService> _logger;

    public UserActivationService(
        UserManager<User> userManager,
        IEmailService emailService,
        ILogger<UserActivationService> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> SendActivationEmailAsync(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        Guid? tenantId = null)
    {
        try
        {
            // Récupérer l'utilisateur
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning(
                    "Cannot send activation email: User {UserId} not found",
                    userId);
                return false;
            }

            // Générer le token d'activation via ASP.NET Identity
            var activationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            _logger.LogInformation(
                "Generated activation token for user {Email} (UserId: {UserId})",
                email,
                userId);

            // Envoyer l'email d'activation
            var emailSent = await _emailService.SendActivationEmailAsync(
                email,
                firstName,
                lastName,
                activationToken,
                userId,
                tenantId);

            if (emailSent)
            {
                _logger.LogInformation(
                    "Activation email sent successfully to {Email} (UserId: {UserId})",
                    email,
                    userId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send activation email to {Email} (UserId: {UserId})",
                    email,
                    userId);
            }

            return emailSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending activation email to {Email} (UserId: {UserId})",
                email,
                userId);
            return false;
        }
    }

    public async Task<bool> ActivateUserAsync(
        Guid userId,
        string activationToken,
        string newPassword)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning(
                    "Cannot activate user: User {UserId} not found",
                    userId);
                return false;
            }

            // Confirmer l'email avec le token
            var confirmResult = await _userManager.ConfirmEmailAsync(user, activationToken);
            if (!confirmResult.Succeeded)
            {
                _logger.LogWarning(
                    "Email confirmation failed for user {UserId}: {Errors}",
                    userId,
                    string.Join(", ", confirmResult.Errors.Select(e => e.Description)));
                return false;
            }

            // Définir le mot de passe
            var passwordResult = await _userManager.AddPasswordAsync(user, newPassword);
            if (!passwordResult.Succeeded)
            {
                _logger.LogWarning(
                    "Password setup failed for user {UserId}: {Errors}",
                    userId,
                    string.Join(", ", passwordResult.Errors.Select(e => e.Description)));
                return false;
            }

            // Activer l'utilisateur au niveau du domaine
            user.Activate();
            var updateResult = await _userManager.UpdateAsync(user);

            if (updateResult.Succeeded)
            {
                _logger.LogInformation(
                    "User {UserId} activated successfully",
                    userId);
                return true;
            }
            else
            {
                _logger.LogWarning(
                    "Failed to update user status for {UserId}: {Errors}",
                    userId,
                    string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error activating user {UserId}",
                userId);
            return false;
        }
    }
}
