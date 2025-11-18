namespace Johodp.Infrastructure.IdentityServer;

using Duende.IdentityServer.Models;

/// <summary>
/// Configuration IdentityServer pour le projet Johodp
/// </summary>
public static class IdentityServerConfig
{
    /// <summary>
    /// Scopes disponibles
    /// </summary>
    public static IEnumerable<ApiScope> GetApiScopes()
    {
        return new List<ApiScope>
        {
            // Application API scopes only. Identity scopes (openid/profile/email) must
            // be declared as IdentityResources (GetIdentityResources) and not duplicated here.
            new ApiScope("johodp.api", "Johodp API")
        };
    }

    /// <summary>
    /// Resources API
    /// </summary>
    public static IEnumerable<ApiResource> GetApiResources()
    {
        return new List<ApiResource>
        {
            new ApiResource("johodp", "Johodp API")
            {
                Scopes = { "johodp.api" },
                UserClaims = { "sub", "email" }
            }
        };
    }

    /// <summary>
    /// Identity Resources
    /// </summary>
    public static IEnumerable<IdentityResource> GetIdentityResources()
    {
        return new List<IdentityResource>
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        };
    }

    /// <summary>
    /// Clients OAuth2/OIDC
    /// </summary>
    public static IEnumerable<Client> GetClients()
    {
        return new List<Client>
        {
            // Swagger UI client
            new Client
            {
                ClientId = "swagger-ui",
                ClientName = "Swagger UI",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false, // public client (swagger UI)
                AllowAccessTokensViaBrowser = true,
                RedirectUris = new List<string>
                {
                    "https://localhost:5001/swagger/oauth2-redirect.html"
                },
                AllowedCorsOrigins = new List<string>
                {
                    "https://localhost:5001"
                },
                AllowedScopes = new List<string> { "openid", "profile", "email", "johodp.api" },
                // enable refresh tokens if needed by clients using offline access
                AllowOfflineAccess = false
            },

            // Machine to Machine client (pour les services backend)
            new Client
            {
                ClientId = "johodp-client-credentials",
                ClientName = "Johodp Client Credentials",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets =
                {
                    new Secret("super-secret-key".Sha256())
                },
                AllowedScopes = { "johodp.api" }
            }
            ,

            // Example SPA client using PKCE
            new Client
            {
                ClientId = "johodp-spa",
                ClientName = "Johodp SPA (PKCE)",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                AllowAccessTokensViaBrowser = true,
                RedirectUris = new List<string>
                {
                    "http://localhost:4200/callback",
                    "http://localhost:4200/"
                },
                PostLogoutRedirectUris = new List<string>
                {
                    "http://localhost:4200/"
                },
                AllowedCorsOrigins = new List<string>
                {
                    "http://localhost:4200"
                },
                AllowedScopes = new List<string> { "openid", "profile", "email", "johodp.api" },
                AllowOfflineAccess = true
            }
        };
    }
}
