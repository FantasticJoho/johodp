namespace Johodp.Domain.Tenants.Specifications;

using Johodp.Domain.Common.Specifications;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Clients.ValueObjects;

/// <summary>
/// EXAMPLE: Specification for active tenants
/// </summary>
public class ActiveTenantSpecification : Specification<Tenant>
{
    public ActiveTenantSpecification()
    {
        Criteria = tenant => tenant.IsActive;
    }
}

/// <summary>
/// EXAMPLE: Specification for tenants with a specific client
/// </summary>
public class TenantByClientSpecification : Specification<Tenant>
{
    public TenantByClientSpecification(ClientId clientId)
    {
        Criteria = tenant => tenant.ClientId != null && tenant.ClientId == clientId;
    }
}

/// <summary>
/// EXAMPLE: Specification for tenants with a specific CustomConfiguration
/// </summary>
public class TenantByCustomConfigSpecification : Specification<Tenant>
{
    public TenantByCustomConfigSpecification(Guid customConfigId)
    {
        Criteria = tenant => tenant.CustomConfigurationId != null && tenant.CustomConfigurationId.Value == customConfigId;
    }
}

/// <summary>
/// EXAMPLE: Specification for tenants with name containing search term
/// </summary>
public class TenantByNameSearchSpecification : Specification<Tenant>
{
    public TenantByNameSearchSpecification(string searchTerm)
    {
        Criteria = tenant => tenant.Name.Contains(searchTerm.ToLowerInvariant());
    }
}

/// <summary>
/// EXAMPLE: Complex specification combining multiple criteria
/// Usage:
/// <code>
/// // Find active tenants for a specific client
/// var spec = new ActiveTenantSpecification()
///     .And(new TenantByClientSpecification(clientId));
/// 
/// var tenants = await _repository.ListAsync(spec);
/// 
/// // Or using fluent API:
/// var spec2 = new TenantByNameSearchSpecification("acme")
///     .And(new ActiveTenantSpecification())
///     .Or(new TenantByCustomConfigSpecification(configId));
/// 
/// // Negation:
/// var inactiveTenants = new ActiveTenantSpecification().Not();
/// </code>
/// </summary>
public class TenantSpecificationExamples
{
    /// <summary>
    /// Example: Active tenants for a specific client
    /// </summary>
    public static Specification<Tenant> ActiveTenantsForClient(ClientId clientId)
    {
        return new ActiveTenantSpecification()
            .And(new TenantByClientSpecification(clientId));
    }

    /// <summary>
    /// Example: Tenants matching search term OR using specific config
    /// </summary>
    public static Specification<Tenant> SearchOrByConfig(string searchTerm, Guid? customConfigId)
    {
        var searchSpec = new TenantByNameSearchSpecification(searchTerm);
        
        if (customConfigId.HasValue)
        {
            return searchSpec.Or(new TenantByCustomConfigSpecification(customConfigId.Value));
        }
        
        return searchSpec;
    }

    /// <summary>
    /// Example: Inactive tenants NOT using a specific config
    /// </summary>
    public static Specification<Tenant> InactiveTenantsWithoutConfig(Guid customConfigId)
    {
        return new ActiveTenantSpecification()
            .Not()
            .And(new TenantByCustomConfigSpecification(customConfigId).Not());
    }
}
