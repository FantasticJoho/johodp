namespace Johodp.Infrastructure.IdentityServer;

using IdentityServer4.Models;

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
            new ApiScope("johodp.api", "Johodp API"),
            new ApiScope("openid", "OpenID Connect"),
            new ApiScope("profile", "User Profile"),
            new ApiScope("email", "User Email")
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
                RequireClientSecret = false,
                RedirectUris = new List<string> { "https://localhost:5001/swagger/oauth2-redirect.html" },
                AllowedScopes = new List<string> { "openid", "profile", "email", "johodp.api" }
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
        };
    }
}
