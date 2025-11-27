namespace Johodp.Infrastructure.IdentityServer;

using Duende.IdentityServer.Models;

/// <summary>
/// Static configuration for IdentityServer resources and scopes.
/// Clients are loaded dynamically from database via CustomClientStore.
/// </summary>
public static class IdentityServerConfig
{
    // ========================================================================
    // API SCOPES
    // ========================================================================
    
    /// <summary>
    /// Defines available API scopes for resource access.
    /// Note: Identity scopes (openid, profile, email) are defined as IdentityResources.
    /// </summary>
    public static IEnumerable<ApiScope> GetApiScopes()
    {
        return new List<ApiScope>
        {
            new ApiScope("johodp.api", "Johodp API")
        };
    }

    // ========================================================================
    // API RESOURCES
    // ========================================================================
    
    /// <summary>
    /// Defines API resources and their associated scopes and claims.
    /// </summary>
    public static IEnumerable<ApiResource> GetApiResources()
    {
        return new List<ApiResource>
        {
            new ApiResource("johodp", "Johodp API")
            {
                Scopes = { "johodp.api" },
                UserClaims = { 
                    "sub",           // User ID
                    "email",         // Email address
                    "tenant_id",     // Tenant identifier
                    "tenant_role",   // User role for specific tenant
                    "tenant_scope",  // User scope/permissions for specific tenant
                    "role",          // Legacy role claim
                    "permission"     // Legacy permission claim
                }
            }
        };
    }

    // ========================================================================
    // IDENTITY RESOURCES (OpenID Connect)
    // ========================================================================
    
    /// <summary>
    /// Defines identity resources (user profile information) available via OpenID Connect.
    /// </summary>
    public static IEnumerable<IdentityResource> GetIdentityResources()
    {
        return new List<IdentityResource>
        {
            // Standard OpenID Connect scopes
            new IdentityResources.OpenId(),    // sub claim
            new IdentityResources.Profile(),   // name, family_name, given_name, etc.
            new IdentityResources.Email(),     // email, email_verified
            
            // Custom identity resource for Johodp-specific claims
            new IdentityResource(
                name: "johodp.identity",
                userClaims: new[] { "tenant_id", "tenant_role", "tenant_scope", "role", "permission" },
                displayName: "Johodp Identity Information"
            )
        };
    }

    // ========================================================================
    // STATIC CLIENTS (Development/Testing)
    // ========================================================================
    
    /// <summary>
    /// Static client configurations for development and testing.
    /// Production clients should be created via API and stored in database.
    /// </summary>
    public static IEnumerable<Client> GetClients()
    {
        return new List<Client>
        {
            // --------------------------------------------------------------------
            // Swagger UI Client (API Documentation)
            // --------------------------------------------------------------------
            new Client
            {
                ClientId = "swagger-ui",
                ClientName = "Swagger UI",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false, // Public client
                AllowAccessTokensViaBrowser = true,
                
                RedirectUris = new List<string>
                {
                    "https://localhost:5001/swagger/oauth2-redirect.html"
                },
                AllowedCorsOrigins = new List<string>
                {
                    "https://localhost:5001"
                },
                AllowedScopes = new List<string> 
                { 
                    "openid", 
                    "profile", 
                    "email", 
                    "johodp.identity", 
                    "johodp.api" 
                },
                AllowOfflineAccess = false
            },

            // --------------------------------------------------------------------
            // Machine-to-Machine Client (Backend Services)
            // --------------------------------------------------------------------
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
            },

            // --------------------------------------------------------------------
            // Example SPA Client (Single Page Application with PKCE)
            // --------------------------------------------------------------------
            new Client
            {
                ClientId = "johodp-spa",
                ClientName = "Johodp SPA (PKCE)",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false, // Public client
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
                AllowedScopes = new List<string> 
                { 
                    "openid", 
                    "profile", 
                    "email", 
                    "johodp.identity", 
                    "johodp.api" 
                },
                
                // Enable refresh tokens for long-lived sessions
                AllowOfflineAccess = true
            }
        };
    }
}
