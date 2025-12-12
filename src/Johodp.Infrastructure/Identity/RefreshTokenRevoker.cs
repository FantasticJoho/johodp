namespace Johodp.Infrastructure.Identity
{
    using System;
    using System.Threading.Tasks;
    using Duende.IdentityServer.Models;
    using Duende.IdentityServer.Stores;

    /// <summary>
    /// Implémentation simple du helper qui révoque les refresh tokens via l'IPersistedGrantStore.
    /// </summary>
    public class RefreshTokenRevoker : IRefreshTokenRevoker
    {
        private readonly IPersistedGrantStore _persistedGrantStore;

        public RefreshTokenRevoker(IPersistedGrantStore persistedGrantStore)
        {
            _persistedGrantStore = persistedGrantStore ?? throw new ArgumentNullException(nameof(persistedGrantStore));
        }

        /// <inheritdoc />
        public async Task RevokeRefreshTokensAsync(string subjectId, string clientId = null)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return;
            }

            var filter = new PersistedGrantFilter
            {
                SubjectId = subjectId,
                Type = "refresh_token",
                ClientId = clientId
            };

            await _persistedGrantStore.RemoveAllAsync(filter).ConfigureAwait(false);
        }
    }
}
