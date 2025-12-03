namespace Johodp.Application.Tenants.Commands.Examples;

using Johodp.Application.Common.Handlers;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Contracts.Tenants;
using Johodp.Domain.Tenants.Aggregates;
using Microsoft.Extensions.Logging;

/// <summary>
/// EXAMPLE: CreateTenantCommand using BaseHandler
/// 
/// This demonstrates how to use BaseHandler for automatic:
/// - Request logging
/// - Execution timing
/// - Error handling
/// 
/// To migrate an existing handler:
/// 1. Inherit from BaseHandler<TRequest, TResponse> instead of IRequestHandler
/// 2. Rename Handle() to HandleCore()
/// 3. Remove manual try-catch and logging (handled by base class)
/// 4. Optionally override OnBeforeHandle/OnAfterHandle/OnError for custom behavior
/// </summary>
public class CreateTenantCommandWithBaseHandler : IRequest<Result<TenantDto>>
{
    public CreateTenantDto Data { get; set; } = null!;
}

public class CreateTenantCommandWithBaseHandlerHandler : BaseHandler<CreateTenantCommandWithBaseHandler, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenantCommandWithBaseHandlerHandler(
        ITenantRepository tenantRepository,
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateTenantCommandWithBaseHandlerHandler> logger) 
        : base(logger)
    {
        _tenantRepository = tenantRepository;
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Core business logic - no logging, no try-catch needed
    /// BaseHandler handles all cross-cutting concerns
    /// </summary>
    protected override async Task<Result<TenantDto>> HandleCore(
        CreateTenantCommandWithBaseHandler command, 
        CancellationToken cancellationToken)
    {
        var dto = command.Data;

        // Validation: Check if tenant name already exists
        if (await _tenantRepository.ExistsAsync(dto.Name))
        {
            return Result<TenantDto>.Failure(Error.Conflict(
                "TENANT_ALREADY_EXISTS",
                $"A tenant with name '{dto.Name}' already exists"));
        }

        // Validation: ClientId required
        if (string.IsNullOrWhiteSpace(dto.ClientId))
        {
            return Result<TenantDto>.Failure(Error.Validation(
                "CLIENT_ID_REQUIRED",
                "ClientId is required. A tenant must be associated with an existing client."));
        }

        // Validation: ClientId must be valid GUID
        if (!Guid.TryParse(dto.ClientId, out var clientGuid))
        {
            return Result<TenantDto>.Failure(Error.Validation(
                "INVALID_CLIENT_ID",
                $"ClientId '{dto.ClientId}' is not a valid GUID."));
        }

        // Validation: Client must exist
        var clientId = Johodp.Domain.Clients.ValueObjects.ClientId.From(clientGuid);
        var client = await _clientRepository.GetByIdAsync(clientId);
        if (client == null)
        {
            return Result<TenantDto>.Failure(Error.NotFound(
                "CLIENT_NOT_FOUND",
                $"Client '{dto.ClientId}' does not exist. Please create the client first."));
        }

        // Create tenant aggregate
        var tenant = Tenant.Create(
            dto.Name,
            dto.DisplayName,
            Johodp.Domain.CustomConfigurations.ValueObjects.CustomConfigurationId.From(dto.CustomConfigurationId));

        // Associate with client
        if (!string.IsNullOrWhiteSpace(dto.ClientId))
        {
            tenant.SetClient(dto.ClientId);
        }

        // Add URLs if provided
        if (dto.AllowedReturnUrls != null)
        {
            foreach (var url in dto.AllowedReturnUrls)
            {
                tenant.AddAllowedReturnUrl(url);
            }
        }

        if (dto.AllowedCorsOrigins != null)
        {
            foreach (var origin in dto.AllowedCorsOrigins)
            {
                tenant.AddAllowedCorsOrigin(origin);
            }
        }

        // Persist
        await _tenantRepository.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TenantDto>.Success(MapToDto(tenant));
    }

    /// <summary>
    /// Optional: Override to add custom logging after successful creation
    /// </summary>
    protected override Task OnAfterHandle(
        CreateTenantCommandWithBaseHandler request, 
        Result<TenantDto> response, 
        TimeSpan elapsed)
    {
        if (response.IsSuccess)
        {
            _logger.LogInformation(
                "Created tenant '{TenantName}' (ID: {TenantId}) for client '{ClientId}' in {ElapsedMs}ms",
                request.Data.Name,
                response.Value.Id,
                request.Data.ClientId,
                elapsed.TotalMilliseconds);
        }
        
        return base.OnAfterHandle(request, response, elapsed);
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
