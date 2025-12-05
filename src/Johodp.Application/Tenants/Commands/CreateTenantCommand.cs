namespace Johodp.Application.Tenants.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Contracts.Tenants;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.CustomConfigurations.ValueObjects;
using Microsoft.Extensions.Logging;

public class CreateTenantCommand : IRequest<Result<TenantDto>>
{
    public CreateTenantDto Data { get; set; } = null!;
}

public class CreateTenantCommandHandler : BaseHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateTenantCommandHandler> logger) : base(logger)
    {
        _tenantRepository = tenantRepository;
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<Result<TenantDto>> HandleCore(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Data;

        // Check if tenant name already exists
        if (await _tenantRepository.ExistsAsync(dto.Name))
        {
            return Result<TenantDto>.Failure(TenantErrors.AlreadyExists(dto.Name));
        }

        // Validate associated client (required)
        if (string.IsNullOrWhiteSpace(dto.ClientId))
        {
            return Result<TenantDto>.Failure(TenantErrors.ClientIdRequired());
        }

        // Parse ClientId as GUID
        if (!Guid.TryParse(dto.ClientId, out var clientGuid))
        {
            return Result<TenantDto>.Failure(TenantErrors.InvalidClientId(dto.ClientId));
        }

        // Verify that the client exists
        var clientId = Johodp.Domain.Clients.ValueObjects.ClientId.From(clientGuid);
        var client = await _clientRepository.GetByIdAsync(clientId);
        if (client == null)
        {
            return Result<TenantDto>.Failure(TenantErrors.ClientNotFound(dto.ClientId));
        }

        // Validate CustomConfigurationId (required)
        if (dto.CustomConfigurationId == Guid.Empty)
        {
            return Result<TenantDto>.Failure(TenantErrors.CustomConfigRequired());
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
        tenant.SetClient(Johodp.Domain.Clients.ValueObjects.ClientId.From(Guid.Parse(dto.ClientId)));

        // Save tenant
        await _tenantRepository.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        // Update associated client with this tenant ID
        await UpdateAssociatedClientAsync(tenant);

        return Result<TenantDto>.Success(MapToDto(tenant));
    }

    private async Task UpdateAssociatedClientAsync(Tenant tenant)
    {
        // Associate the tenant with the client (bidirectional relationship)
        if (tenant.ClientId == null)
            return;

        var client = await _clientRepository.GetByIdAsync(tenant.ClientId);
        
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
            ClientId = tenant.ClientId?.Value.ToString()
        };
    }
}
