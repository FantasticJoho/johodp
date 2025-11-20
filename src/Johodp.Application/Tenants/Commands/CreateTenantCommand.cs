namespace Johodp.Application.Tenants.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Tenants.DTOs;
using Johodp.Domain.Tenants.Aggregates;

public class CreateTenantCommand : IRequest<TenantDto>
{
    public CreateTenantDto Data { get; set; } = null!;
}

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantDto> Handle(CreateTenantCommand command, CancellationToken cancellationToken = default)
    {
        var dto = command.Data;

        // Check if tenant name already exists
        if (await _tenantRepository.ExistsAsync(dto.Name))
        {
            throw new InvalidOperationException($"A tenant with name '{dto.Name}' already exists");
        }

        // Create tenant aggregate
        var tenant = Tenant.Create(
            dto.Name,
            dto.DisplayName,
            dto.DefaultLanguage ?? "fr-FR");

        // Add supported languages
        if (dto.SupportedLanguages != null)
        {
            foreach (var lang in dto.SupportedLanguages)
            {
                if (lang != tenant.DefaultLanguage)
                {
                    tenant.AddSupportedLanguage(lang);
                }
            }
        }

        // Set branding
        if (dto.PrimaryColor != null || dto.SecondaryColor != null || 
            dto.LogoUrl != null || dto.BackgroundImageUrl != null || dto.CustomCss != null)
        {
            tenant.UpdateBranding(
                dto.PrimaryColor,
                dto.SecondaryColor,
                dto.LogoUrl,
                dto.BackgroundImageUrl,
                dto.CustomCss);
        }

        // Set localization
        if (dto.Timezone != null || dto.Currency != null)
        {
            tenant.UpdateLocalization(dto.Timezone, dto.Currency);
        }

        // Add return URLs
        if (dto.AllowedReturnUrls != null)
        {
            foreach (var url in dto.AllowedReturnUrls)
            {
                tenant.AddAllowedReturnUrl(url);
            }
        }

        // Add associated clients
        if (dto.AssociatedClientIds != null)
        {
            foreach (var clientId in dto.AssociatedClientIds)
            {
                tenant.AddAssociatedClient(clientId);
            }
        }

        // Save tenant
        await _tenantRepository.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        // Update associated clients with return URLs
        await UpdateAssociatedClientsAsync(tenant);

        return MapToDto(tenant);
    }

    private async Task UpdateAssociatedClientsAsync(Tenant tenant)
    {
        foreach (var clientId in tenant.AssociatedClientIds)
        {
            var client = await _clientRepository.GetByNameAsync(clientId);
            if (client != null)
            {
                // Clear existing redirect URIs and add tenant's return URLs
                foreach (var url in tenant.AllowedReturnUrls)
                {
                    // Check if redirect URI already exists
                    if (!client.AllowedRedirectUris.Contains(url))
                    {
                        client.AddRedirectUri(url);
                    }
                }
                await _clientRepository.UpdateAsync(client);
            }
        }
        await _unitOfWork.SaveChangesAsync();
    }

    private static TenantDto MapToDto(Tenant tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id.Value,
            Name = tenant.Name,
            DisplayName = tenant.DisplayName,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            PrimaryColor = tenant.PrimaryColor,
            SecondaryColor = tenant.SecondaryColor,
            LogoUrl = tenant.LogoUrl,
            BackgroundImageUrl = tenant.BackgroundImageUrl,
            CustomCss = tenant.CustomCss,
            DefaultLanguage = tenant.DefaultLanguage,
            SupportedLanguages = tenant.SupportedLanguages.ToList(),
            Timezone = tenant.Timezone,
            Currency = tenant.Currency,
            AllowedReturnUrls = tenant.AllowedReturnUrls.ToList(),
            AssociatedClientIds = tenant.AssociatedClientIds.ToList()
        };
    }
}
