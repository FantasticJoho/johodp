namespace Johodp.Application.Common.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service d'authentification MFA qui vérifie si un client requiert le MFA
/// et gère le flux d'authentification avec Microsoft Authenticator
/// </summary>
public interface IMfaAuthenticationService
{
    /// <summary>
    /// Initie une demande MFA pour un utilisateur
    /// </summary>
    Task<MfaPendingRequest> InitiateMfaAsync(
        Guid userId,
        string email,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide une demande MFA
    /// </summary>
    Task<bool> ValidateMfaAsync(
        Guid requestId,
        string? verificationCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approuve une demande MFA (simulateur Microsoft Authenticator)
    /// </summary>
    Task ApproveMfaAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejette une demande MFA
    /// </summary>
    Task RejectMfaAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le statut d'une demande MFA
    /// </summary>
    Task<MfaPendingRequest?> GetMfaRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Représente une demande MFA en attente
/// </summary>
public class MfaPendingRequest
{
    public Guid RequestId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public MfaRequestStatus Status { get; set; }
}

public enum MfaRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Expired
}
