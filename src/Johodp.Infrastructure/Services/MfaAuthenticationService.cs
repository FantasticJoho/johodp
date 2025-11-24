namespace Johodp.Infrastructure.Services;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Johodp.Application.Common.Interfaces;

/// <summary>
/// Service d'authentification MFA pour valider les connexions avec Microsoft Authenticator
/// Ce service gère le flux MFA uniquement si le client requiert le MFA
/// </summary>
public class MfaAuthenticationService : IMfaAuthenticationService
{
    private readonly ILogger<MfaAuthenticationService> _logger;
    private readonly ConcurrentDictionary<Guid, MfaPendingRequest> _pendingRequests = new();

    public MfaAuthenticationService(ILogger<MfaAuthenticationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Génère une demande MFA pour un utilisateur
    /// </summary>
    public Task<MfaPendingRequest> InitiateMfaAsync(
        Guid userId,
        string email,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var request = new MfaPendingRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            ClientId = clientId,
            VerificationCode = GenerateVerificationCode(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = MfaRequestStatus.Pending
        };

        _pendingRequests[request.RequestId] = request;

        _logger.LogInformation(
            "MFA request initiated for user {UserId} with request ID {RequestId} for client {ClientId}",
            userId, request.RequestId, clientId);

        // TODO: Envoyer push notification à Microsoft Authenticator
        // await SendMicrosoftAuthenticatorPushAsync(request);

        return Task.FromResult(request);
    }

    /// <summary>
    /// Vérifie si une demande MFA a été validée
    /// </summary>
    public Task<bool> ValidateMfaAsync(
        Guid requestId,
        string? verificationCode = null,
        CancellationToken cancellationToken = default)
    {
        if (!_pendingRequests.TryGetValue(requestId, out var request))
        {
            _logger.LogWarning("MFA validation failed: Request {RequestId} not found", requestId);
            return Task.FromResult(false);
        }

        if (request.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("MFA validation failed: Request {RequestId} has expired", requestId);
            _pendingRequests.TryRemove(requestId, out _);
            return Task.FromResult(false);
        }

        // Si verification code fourni, valider le code
        if (!string.IsNullOrEmpty(verificationCode))
        {
            if (request.VerificationCode != verificationCode)
            {
                _logger.LogWarning(
                    "MFA validation failed: Invalid verification code for request {RequestId}",
                    requestId);
                return Task.FromResult(false);
            }
        }

        // Vérifier si la demande a été approuvée (push notification)
        if (request.Status == MfaRequestStatus.Approved)
        {
            _pendingRequests.TryRemove(requestId, out _);
            _logger.LogInformation(
                "MFA validation successful for user {UserId} with request {RequestId}",
                request.UserId, requestId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Approuve une demande MFA (appelé par le simulateur Microsoft Authenticator)
    /// </summary>
    public Task ApproveMfaAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (_pendingRequests.TryGetValue(requestId, out var request))
        {
            request.Status = MfaRequestStatus.Approved;
            _logger.LogInformation("MFA request {RequestId} approved for user {UserId}", 
                requestId, request.UserId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rejette une demande MFA
    /// </summary>
    public Task RejectMfaAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (_pendingRequests.TryGetValue(requestId, out var request))
        {
            request.Status = MfaRequestStatus.Rejected;
            _pendingRequests.TryRemove(requestId, out _);
            _logger.LogInformation("MFA request {RequestId} rejected for user {UserId}",
                requestId, request.UserId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Récupère le statut d'une demande MFA
    /// </summary>
    public Task<MfaPendingRequest?> GetMfaRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        _pendingRequests.TryGetValue(requestId, out var request);
        return Task.FromResult<MfaPendingRequest?>(request);
    }

    private static string GenerateVerificationCode()
    {
        // Génère un code à 6 chiffres
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}
