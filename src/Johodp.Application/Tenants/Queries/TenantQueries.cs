namespace Johodp.Application.Tenants.Queries;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Tenants.DTOs;
using Johodp.Domain.Tenants.ValueObjects;

public class GetTenantByIdQuery : IRequest<TenantDto?>
{
    public Guid TenantId { get; set; }
}

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByIdQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantDto?> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId.From(query.TenantId);
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
            return null;

        return MapToDto(tenant);
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

public class GetTenantByNameQuery : IRequest<TenantDto?>
{
    public string Name { get; set; } = string.Empty;
}

public class GetTenantByNameQueryHandler : IRequestHandler<GetTenantByNameQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByNameQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantDto?> Handle(GetTenantByNameQuery query, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByNameAsync(query.Name);

        if (tenant == null)
            return null;

        return MapToDto(tenant);
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
