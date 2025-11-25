using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace Johodp.Infrastructure.Services;

public interface ITotpService
{
    string GenerateSecret(int bytes = 20);
    string GetProvisioningUri(string issuer, string accountName, string secret, int digits = 6, int period = 30);
    string GenerateQrCodeBase64(string provisioningUri);
    bool ValidateCode(string secret, string code, int digits = 6, int period = 30, int toleranceSteps = 1);
}

public class TotpService : ITotpService
{
    public string GenerateSecret(int bytes = 20)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Base32Encode(buffer);
    }

    public string GetProvisioningUri(string issuer, string accountName, string secret, int digits = 6, int period = 30)
    {
        issuer = Uri.EscapeDataString(issuer);
        accountName = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{issuer}:{accountName}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits={digits}&period={period}";
    }

    public string GenerateQrCodeBase64(string provisioningUri)
    {
        using var qrGen = new QRCodeGenerator();
        var data = qrGen.CreateQrCode(provisioningUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        var pngBytes = qrCode.GetGraphic(6);
        return Convert.ToBase64String(pngBytes);
    }

    public bool ValidateCode(string secret, string code, int digits = 6, int period = 30, int toleranceSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;
        if (!int.TryParse(code, out var provided)) return false;
        var key = Base32Decode(secret);
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var step = unix / period;
        for (var i = -toleranceSteps; i <= toleranceSteps; i++)
        {
            var totp = ComputeTotp(key, (ulong)(step + i), digits);
            if (totp == provided) return true;
        }
        return false;
    }

    private static int ComputeTotp(byte[] key, ulong timestepNumber, int digits)
    {
        var counterBytes = BitConverter.GetBytes(timestepNumber);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7f) << 24)
                         | ((hash[offset + 1] & 0xff) << 16)
                         | ((hash[offset + 2] & 0xff) << 8)
                         | (hash[offset + 3] & 0xff);
        var totp = binaryCode % (int)Math.Pow(10, digits);
        return totp;
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder();
        int bits = 0, value = 0;
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(value >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
        {
            output.Append(alphabet[(value << (5 - bits)) & 31]);
        }
        return output.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Trim('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int bits = 0, value = 0;
        foreach (var c in cleaned)
        {
            var idx = alphabet.IndexOf(c);
            if (idx < 0) continue;
            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return bytes.ToArray();
    }
}