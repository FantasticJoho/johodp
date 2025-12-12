namespace Johodp.Infrastructure.Identity
{
    using System.Threading.Tasks;

    /// <summary>
    /// Service helper pour révoquer les refresh tokens (persisted grants) d'un utilisateur.
    /// </summary>
    public interface IRefreshTokenRevoker
    {
        /// <summary>
        /// Révoque tous les refresh tokens pour un utilisateur (optionnellement restreint à un client).
        /// </summary>
        /// <param name="subjectId">L'identifiant de l'utilisateur (subject).</param>
        /// <param name="clientId">ClientId optionnel pour restreindre la révocation.</param>
        Task RevokeRefreshTokensAsync(string subjectId, string clientId = null);
    }
}
