using System.Text;
using System.Text.Encodings.Web;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Clients;
using Johodp.Domain.Tenants;
using Johodp.Domain.Users;
using Johodp.Domain.Users.Aggregates;

namespace Johodp.Application.Users;

/// <summary>
/// Implementation of MFA/TOTP-related business logic.
/// This service encapsulates complex domain logic that involves multiple aggregates
/// (User, Tenant, Client) to keep controllers thin and focused on HTTP concerns.
/// </summary>
public class MfaService : IMfaService
{
    private readonly IClientRepository _clientRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly UrlEncoder _urlEncoder;

    public MfaService(
        IClientRepository clientRepository,
        ITenantRepository tenantRepository,
        UrlEncoder urlEncoder)
    {
        _clientRepository = clientRepository;
        _tenantRepository = tenantRepository;
        _urlEncoder = urlEncoder;
    }

    /// <inheritdoc />
    public async Task<bool> IsMfaRequiredForUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user.TenantId == null) return false;

        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId);
        if (tenant?.ClientId == null) return false;

        var client = await _clientRepository.GetByIdAsync(tenant.ClientId);
        return client?.RequireMfa ?? false;
    }

    /// <inheritdoc />
    public string GenerateQrCodeUri(string email, string unformattedKey, string issuer)
    {
        return $"otpauth://totp/{_urlEncoder.Encode(issuer)}:{_urlEncoder.Encode(email)}?secret={unformattedKey}&issuer={_urlEncoder.Encode(issuer)}";
    }

    /// <inheritdoc />
    public string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }
}
