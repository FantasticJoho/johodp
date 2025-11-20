namespace Johodp.Application.Tenants.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Tenants.DTOs;
using Johodp.Domain.Tenants.ValueObjects;

public class UpdateTenantCommand : IRequest<TenantDto>
{
    public Guid TenantId { get; set; }
    public UpdateTenantDto Data { get; set; } = null!;
}

public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantDto> Handle(UpdateTenantCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId.From(command.TenantId);
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with ID '{command.TenantId}' not found");
        }

        var dto = command.Data;
        var returnUrlsChanged = false;

        // Update display name
        if (dto.DisplayName != null)
        {
            tenant.UpdateDisplayName(dto.DisplayName);
        }

        // Update branding
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

        // Update default language
        if (dto.DefaultLanguage != null)
        {
            tenant.SetDefaultLanguage(dto.DefaultLanguage);
        }

        // Update supported languages (replace all)
        if (dto.SupportedLanguages != null)
        {
            // Remove languages not in the new list
            var currentLanguages = tenant.SupportedLanguages.ToList();
            foreach (var lang in currentLanguages)
            {
                if (!dto.SupportedLanguages.Contains(lang) && lang != tenant.DefaultLanguage)
                {
                    tenant.RemoveSupportedLanguage(lang);
                }
            }

            // Add new languages
            foreach (var lang in dto.SupportedLanguages)
            {
                tenant.AddSupportedLanguage(lang);
            }
        }

        // Update localization
        if (dto.Timezone != null || dto.Currency != null)
        {
            tenant.UpdateLocalization(dto.Timezone, dto.Currency);
        }

        // Update return URLs (replace all)
        if (dto.AllowedReturnUrls != null)
        {
            returnUrlsChanged = true;
            
            // Remove all existing return URLs
            var currentUrls = tenant.AllowedReturnUrls.ToList();
            foreach (var url in currentUrls)
            {
                tenant.RemoveAllowedReturnUrl(url);
            }

            // Add new return URLs
            foreach (var url in dto.AllowedReturnUrls)
            {
                tenant.AddAllowedReturnUrl(url);
            }
        }

        // Update associated clients (replace all)
        if (dto.AssociatedClientIds != null)
        {
            // Remove all existing client associations
            var currentClients = tenant.AssociatedClientIds.ToList();
            foreach (var clientId in currentClients)
            {
                tenant.RemoveAssociatedClient(clientId);
            }

            // Add new client associations
            foreach (var clientId in dto.AssociatedClientIds)
            {
                tenant.AddAssociatedClient(clientId);
            }

            returnUrlsChanged = true; // Need to update clients
        }

        // Update active status
        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
                tenant.Activate();
            else
                tenant.Deactivate();
        }

        // Save tenant
        await _tenantRepository.UpdateAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        // Update associated clients if return URLs or client associations changed
        if (returnUrlsChanged)
        {
            await UpdateAssociatedClientsAsync(tenant);
        }

        return MapToDto(tenant);
    }

    private async Task UpdateAssociatedClientsAsync(Domain.Tenants.Aggregates.Tenant tenant)
    {
        foreach (var clientId in tenant.AssociatedClientIds)
        {
            var client = await _clientRepository.GetByNameAsync(clientId);
            if (client != null)
            {
                // Remove all redirect URIs and add tenant's return URLs
                var currentUris = client.AllowedRedirectUris.ToList();
                foreach (var uri in currentUris)
                {
                    client.RemoveRedirectUri(uri);
                }

                // Add tenant's return URLs
                foreach (var url in tenant.AllowedReturnUrls)
                {
                    client.AddRedirectUri(url);
                }

                await _clientRepository.UpdateAsync(client);
            }
        }
        await _unitOfWork.SaveChangesAsync();
    }

    private static TenantDto MapToDto(Domain.Tenants.Aggregates.Tenant tenant)
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
