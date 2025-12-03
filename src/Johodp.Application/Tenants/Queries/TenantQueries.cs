namespace Johodp.Application.Tenants.Queries;

using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Contracts.Tenants;
using Johodp.Domain.Tenants.ValueObjects;
using Microsoft.Extensions.Logging;

public class GetTenantByIdQuery : IRequest<Result<TenantDto>>
{
    public Guid TenantId { get; set; }
}

public class GetTenantByIdQueryHandler : BaseHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByIdQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetTenantByIdQueryHandler> logger) : base(logger)
    {
        _tenantRepository = tenantRepository;
    }

    protected override async Task<Result<TenantDto>> HandleCore(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(query.TenantId);
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
        {
            return Result<TenantDto>.Failure(TenantErrors.NotFound(query.TenantId));
        }

        return Result<TenantDto>.Success(MapToDto(tenant));
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

public class GetAllTenantsQuery : IRequest<IEnumerable<TenantDto>> { }

public class GetAllTenantsQueryHandler : BaseHandler<GetAllTenantsQuery, IEnumerable<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetAllTenantsQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetAllTenantsQueryHandler> logger) : base(logger)
    {
        _tenantRepository = tenantRepository;
    }

    protected override async Task<IEnumerable<TenantDto>> HandleCore(GetAllTenantsQuery query, CancellationToken cancellationToken)
    {
        var tenants = await _tenantRepository.GetAllAsync();
        return tenants.Select(MapToDto);
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

public class GetTenantByNameQuery : IRequest<Result<TenantDto>>
{
    public string TenantName { get; set; } = string.Empty;
}

public class GetTenantByNameQueryHandler : BaseHandler<GetTenantByNameQuery, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByNameQueryHandler(
        ITenantRepository tenantRepository,
        ILogger<GetTenantByNameQueryHandler> logger) : base(logger)
    {
        _tenantRepository = tenantRepository;
    }

    protected override async Task<Result<TenantDto>> HandleCore(GetTenantByNameQuery query, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByNameAsync(query.TenantName);

        if (tenant == null)
        {
            return Result<TenantDto>.Failure(TenantErrors.NotFoundByName(query.TenantName));
        }

        return Result<TenantDto>.Success(MapToDto(tenant));
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
