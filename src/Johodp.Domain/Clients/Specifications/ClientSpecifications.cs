namespace Johodp.Domain.Clients.Specifications;

using Johodp.Domain.Common.Specifications;
using Johodp.Domain.Clients.Aggregates;

/// <summary>
/// Specification for active clients
/// </summary>
public class ActiveClientSpecification : Specification<Client>
{
    public ActiveClientSpecification()
    {
        Criteria = client => client.IsActive;
    }
}

/// <summary>
/// Specification for clients with associated tenants
/// </summary>
public class ClientWithTenantsSpecification : Specification<Client>
{
    public ClientWithTenantsSpecification()
    {
        Criteria = client => client.AssociatedTenantIds.Any();
    }
}

/// <summary>
/// Specification for clients by name search
/// </summary>
public class ClientByNameSearchSpecification : Specification<Client>
{
    public ClientByNameSearchSpecification(string searchTerm)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();
        Criteria = client => client.ClientName.ToLower().Contains(lowerSearch);
    }
}

/// <summary>
/// Example usage patterns for Client specifications
/// </summary>
public static class ClientSpecificationExamples
{
    /// <summary>
    /// Get active clients with tenants (clients in use)
    /// </summary>
    public static Specification<Client> ActiveClientsInUse()
    {
        return new ActiveClientSpecification()
            .And(new ClientWithTenantsSpecification());
    }

    /// <summary>
    /// Get active clients without tenants (unused clients)
    /// </summary>
    public static Specification<Client> ActiveClientsNotInUse()
    {
        return new ActiveClientSpecification()
            .And(new ClientWithTenantsSpecification().Not());
    }

    /// <summary>
    /// Search active clients by name
    /// </summary>
    public static Specification<Client> SearchActiveClients(string searchTerm)
    {
        return new ActiveClientSpecification()
            .And(new ClientByNameSearchSpecification(searchTerm));
    }

    /// <summary>
    /// Get clients without tenants (candidates for cleanup)
    /// </summary>
    public static Specification<Client> ClientsWithoutTenants()
    {
        return new ClientWithTenantsSpecification().Not();
    }

    /// <summary>
    /// Get inactive clients without tenants (safe to delete)
    /// </summary>
    public static Specification<Client> SafeToDeleteClients()
    {
        return new ActiveClientSpecification().Not()
            .And(new ClientWithTenantsSpecification().Not());
    }
}
