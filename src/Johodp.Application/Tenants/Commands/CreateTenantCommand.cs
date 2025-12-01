namespace Johodp.Application.Tenants.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Tenants.DTOs;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.CustomConfigurations.ValueObjects;

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

        // Validate associated client (required)
        if (string.IsNullOrWhiteSpace(dto.ClientId))
        {
            throw new InvalidOperationException("ClientId is required. A tenant must be associated with an existing client.");
        }

        // Parse ClientId as GUID
        if (!Guid.TryParse(dto.ClientId, out var clientGuid))
        {
            throw new InvalidOperationException($"ClientId '{dto.ClientId}' is not a valid GUID.");
        }

        // Verify that the client exists
        var clientId = Johodp.Domain.Clients.ValueObjects.ClientId.From(clientGuid);
        var client = await _clientRepository.GetByIdAsync(clientId);
        if (client == null)
        {
            throw new InvalidOperationException($"Client '{dto.ClientId}' does not exist. Please create the client first.");
        }

        // Validate CustomConfigurationId (required)
        if (dto.CustomConfigurationId == Guid.Empty)
        {
            throw new InvalidOperationException("CustomConfigurationId is required. A tenant must reference a CustomConfiguration.");
        }

        // Create tenant aggregate with required CustomConfigurationId
        var customConfigId = CustomConfigurationId.From(dto.CustomConfigurationId);

        var tenant = Tenant.Create(
            dto.Name,
            dto.DisplayName,
            customConfigId);

        // Add return URLs
        if (dto.AllowedReturnUrls != null)
        {
            foreach (var url in dto.AllowedReturnUrls)
            {
                tenant.AddAllowedReturnUrl(url);
            }
        }

        // Add CORS origins
        if (dto.AllowedCorsOrigins != null)
        {
            foreach (var origin in dto.AllowedCorsOrigins)
            {
                tenant.AddAllowedCorsOrigin(origin);
            }
        }

        // Set associated client (required)
        tenant.SetClient(dto.ClientId);

        // Save tenant
        await _tenantRepository.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        // Update associated client with this tenant ID
        await UpdateAssociatedClientAsync(tenant);

        return MapToDto(tenant);
    }

    private async Task UpdateAssociatedClientAsync(Tenant tenant)
    {
        // Associate the tenant with the client (bidirectional relationship)
        // Parse ClientId as GUID to retrieve the client
        if (!Guid.TryParse(tenant.ClientId, out var clientGuid))
        {
            throw new InvalidOperationException($"Invalid ClientId format: {tenant.ClientId}");
        }

        var clientId = Johodp.Domain.Clients.ValueObjects.ClientId.From(clientGuid);
        var client = await _clientRepository.GetByIdAsync(clientId);
        
        if (client != null)
        {
            // Associate tenant with client if not already associated
            if (!client.AssociatedTenantIds.Contains(tenant.Id.Value.ToString()))
            {
                client.AssociateTenant(tenant.Id.Value.ToString());
                await _clientRepository.UpdateAsync(client);
                await _unitOfWork.SaveChangesAsync();
            }
        }
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
            CustomConfigurationId = tenant.CustomConfigurationId.Value,
            AllowedReturnUrls = tenant.AllowedReturnUrls.ToList(),
            AllowedCorsOrigins = tenant.AllowedCorsOrigins.ToList(),
            ClientId = tenant.ClientId
        };
    }
}
