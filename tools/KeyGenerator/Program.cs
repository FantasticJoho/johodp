using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

Console.WriteLine("🔑 IdentityServer JWK Signing Key Generator");
Console.WriteLine("===========================================\n");

// Parse arguments
var outputPath = "signing-key.jwk";
var keySize = 2048;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--output" && i + 1 < args.Length)
        outputPath = args[i + 1];
    else if (args[i] == "--keysize" && i + 1 < args.Length)
        keySize = int.Parse(args[i + 1]);
}

Console.WriteLine($"📋 Configuration:");
Console.WriteLine($"   Output: {outputPath}");
Console.WriteLine($"   Key Size: {keySize} bits");
Console.WriteLine();

// Generate RSA key
Console.WriteLine("⚙️  Generating RSA key pair...");
var rsa = RSA.Create(keySize);
var keyId = Guid.NewGuid().ToString();

var key = new RsaSecurityKey(rsa)
{
    KeyId = keyId
};

Console.WriteLine($"✅ RSA key generated (kid: {keyId})");
Console.WriteLine();

// Export parameters
Console.WriteLine("📦 Exporting key parameters...");
var parameters = rsa.ExportParameters(includePrivateParameters: true);

// Create JWK object (RFC 7517)
var jwk = new
{
    kty = "RSA",
    kid = keyId,
    use = "sig",
    alg = "RS256",
    n = Base64UrlEncoder.Encode(parameters.Modulus!),
    e = Base64UrlEncoder.Encode(parameters.Exponent!),
    d = Base64UrlEncoder.Encode(parameters.D!),
    p = Base64UrlEncoder.Encode(parameters.P!),
    q = Base64UrlEncoder.Encode(parameters.Q!),
    dp = Base64UrlEncoder.Encode(parameters.DP!),
    dq = Base64UrlEncoder.Encode(parameters.DQ!),
    qi = Base64UrlEncoder.Encode(parameters.InverseQ!)
};

// Serialize to JSON
var options = new JsonSerializerOptions 
{ 
    WriteIndented = true 
};
var jwkJson = JsonSerializer.Serialize(jwk, options);

// Write to file
Console.WriteLine($"💾 Writing to {outputPath}...");
File.WriteAllText(outputPath, jwkJson);

Console.WriteLine();
Console.WriteLine("✅ JWK signing key generated successfully!");
Console.WriteLine();
Console.WriteLine("⚠️  SECURITY WARNING:");
Console.WriteLine("   This file contains PRIVATE key material.");
Console.WriteLine("   - Store it securely (Vault, Key Management Service)");
Console.WriteLine("   - NEVER commit it to Git");
Console.WriteLine("   - Set strict file permissions (chmod 600)");
Console.WriteLine();
Console.WriteLine("📖 Next steps:");
Console.WriteLine("   1. Store in Vault:");
Console.WriteLine($"      vault kv put secret/johodp/identityserver/current @{outputPath}");
Console.WriteLine("   2. Or copy to secure location:");
Console.WriteLine($"      cp {outputPath} /secure/path/");
Console.WriteLine("   3. Delete local copy:");
Console.WriteLine($"      rm {outputPath}");
Console.WriteLine();
