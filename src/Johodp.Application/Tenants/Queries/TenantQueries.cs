namespace Johodp.Application.Tenants.Queries;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Application.Tenants.DTOs;
using Johodp.Domain.Tenants.ValueObjects;

public class GetTenantByIdQuery : IRequest<Result<TenantDto>>
{
    public Guid TenantId { get; set; }
}

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByIdQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken = default)
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

public class GetAllTenantsQueryHandler : IRequestHandler<GetAllTenantsQuery, IEnumerable<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetAllTenantsQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<IEnumerable<TenantDto>> Handle(GetAllTenantsQuery query, CancellationToken cancellationToken = default)
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

public class GetTenantByNameQueryHandler : IRequestHandler<GetTenantByNameQuery, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByNameQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<TenantDto>> Handle(GetTenantByNameQuery query, CancellationToken cancellationToken = default)
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
