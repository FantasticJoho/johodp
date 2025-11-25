using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace Johodp.Infrastructure.IdentityServer;

/// <summary>
/// Helper for loading JWK (JSON Web Key) signing keys from files or Vault.
/// RFC 7517: https://datatracker.ietf.org/doc/html/rfc7517
/// </summary>
public static class SigningKeyHelper
{
    /// <summary>
    /// Load a JWK from a file path.
    /// </summary>
    /// <param name="path">Absolute path to .jwk file</param>
    /// <returns>RSA security key ready for IdentityServer</returns>
    public static RsaSecurityKey LoadJwkFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"JWK file not found: {path}");

        var jwkJson = File.ReadAllText(path);
        return LoadJwkFromJson(jwkJson);
    }

    /// <summary>
    /// Load a JWK from JSON string (e.g., from Vault).
    /// </summary>
    /// <param name="jwkJson">JWK in JSON format</param>
    /// <returns>RSA security key ready for IdentityServer</returns>
    public static RsaSecurityKey LoadJwkFromJson(string jwkJson)
    {
        var jwk = JsonSerializer.Deserialize<JsonElement>(jwkJson);
        
        if (!jwk.TryGetProperty("kty", out var kty) || kty.GetString() != "RSA")
            throw new InvalidOperationException("JWK must be of type RSA");

        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "n")),
            Exponent = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "e")),
            D = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "d")),
            P = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "p")),
            Q = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "q")),
            DP = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "dp")),
            DQ = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "dq")),
            InverseQ = Base64UrlEncoder.DecodeBytes(GetRequiredProperty(jwk, "qi"))
        });
        
        return new RsaSecurityKey(rsa)
        {
            KeyId = jwk.TryGetProperty("kid", out var kid) 
                ? kid.GetString() 
                : Guid.NewGuid().ToString()
        };
    }

    private static string GetRequiredProperty(JsonElement jwk, string propertyName)
    {
        if (!jwk.TryGetProperty(propertyName, out var property))
            throw new InvalidOperationException($"JWK missing required property: {propertyName}");
        
        return property.GetString() 
            ?? throw new InvalidOperationException($"JWK property {propertyName} is null");
    }
}
