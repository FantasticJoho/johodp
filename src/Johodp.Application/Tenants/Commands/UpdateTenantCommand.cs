namespace Johodp.Application.Tenants.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Tenants.DTOs;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Domain.CustomConfigurations.ValueObjects;

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

        // Update display name
        if (dto.DisplayName != null)
        {
            tenant.UpdateDisplayName(dto.DisplayName);
        }

        // Update CustomConfiguration reference (required if provided)
        if (dto.CustomConfigurationId.HasValue)
        {
            if (dto.CustomConfigurationId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("CustomConfigurationId cannot be empty. A tenant must have a valid CustomConfiguration.");
            }

            tenant.SetCustomConfiguration(
                Domain.CustomConfigurations.ValueObjects.CustomConfigurationId.From(dto.CustomConfigurationId.Value));
        }

        // Update return URLs (replace all)
        if (dto.AllowedReturnUrls != null)
        {
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

        // Update CORS origins (replace all)
        if (dto.AllowedCorsOrigins != null)
        {
            // Remove all existing CORS origins
            var currentOrigins = tenant.AllowedCorsOrigins.ToList();
            foreach (var origin in currentOrigins)
            {
                tenant.RemoveAllowedCorsOrigin(origin);
            }

            // Add new CORS origins
            foreach (var origin in dto.AllowedCorsOrigins)
            {
                tenant.AddAllowedCorsOrigin(origin);
            }
        }

        // Update associated client (replace)
        if (dto.ClientId != null)
        {
            // Validate that the client exists if not empty
            if (!string.IsNullOrWhiteSpace(dto.ClientId))
            {
                if (!Guid.TryParse(dto.ClientId, out var clientGuid))
                {
                    throw new InvalidOperationException($"ClientId '{dto.ClientId}' is not a valid GUID.");
                }
                
                var clientId = Johodp.Domain.Clients.ValueObjects.ClientId.From(clientGuid);
                var client = await _clientRepository.GetByIdAsync(clientId);
                if (client == null)
                {
                    throw new InvalidOperationException($"Client '{dto.ClientId}' does not exist. Please create the client first.");
                }
            }

            // Update the client association
            var oldClientId = tenant.ClientId;
            tenant.SetClient(string.IsNullOrWhiteSpace(dto.ClientId) ? null : dto.ClientId);

            // If client changed, update bidirectional associations
            if (oldClientId != tenant.ClientId)
            {
                // Remove tenant from old client
                if (!string.IsNullOrWhiteSpace(oldClientId))
                {
                    if (Guid.TryParse(oldClientId, out var oldClientGuid))
                    {
                        var oldClientId_ValueObject = Johodp.Domain.Clients.ValueObjects.ClientId.From(oldClientGuid);
                        var oldClient = await _clientRepository.GetByIdAsync(oldClientId_ValueObject);
                        if (oldClient != null)
                        {
                            oldClient.DissociateTenant(tenant.Id.Value.ToString());
                            await _clientRepository.UpdateAsync(oldClient);
                        }
                    }
                }

                // Add tenant to new client
                if (!string.IsNullOrWhiteSpace(tenant.ClientId))
                {
                    if (Guid.TryParse(tenant.ClientId, out var newClientGuid))
                    {
                        var newClientId = Johodp.Domain.Clients.ValueObjects.ClientId.From(newClientGuid);
                        var newClient = await _clientRepository.GetByIdAsync(newClientId);
                        if (newClient != null && !newClient.AssociatedTenantIds.Contains(tenant.Id.Value.ToString()))
                        {
                            newClient.AssociateTenant(tenant.Id.Value.ToString());
                            await _clientRepository.UpdateAsync(newClient);
                        }
                    }
                }
            }
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

        // Note: Client redirect URIs are now automatically aggregated from all associated tenants
        // via the CustomClientStore. No need to manually sync returnUrls to clients when they change.
        // The dynamic aggregation happens at runtime when IdentityServer loads the client.

        return MapToDto(tenant);
    }

    private async Task UpdateAssociatedClientsAsync(Domain.Tenants.Aggregates.Tenant tenant)
    {
        // This method is no longer needed since CustomClientStore handles dynamic aggregation.
        // Keeping for backward compatibility but it's now a no-op.
        // Consider removing this method and the returnUrlsChanged logic in future refactoring.
        await Task.CompletedTask;
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
            CustomConfigurationId = tenant.CustomConfigurationId.Value,
            AllowedReturnUrls = tenant.AllowedReturnUrls.ToList(),
            AllowedCorsOrigins = tenant.AllowedCorsOrigins.ToList(),
            ClientId = tenant.ClientId
        };
    }
}
