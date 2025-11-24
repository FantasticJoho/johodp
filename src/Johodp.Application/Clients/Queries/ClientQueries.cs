namespace Johodp.Application.Clients.Queries;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Clients.DTOs;
using Johodp.Domain.Clients.ValueObjects;

public class GetClientByIdQuery : IRequest<ClientDto?>
{
    public Guid ClientId { get; set; }
}

public class GetClientByIdQueryHandler : IRequestHandler<GetClientByIdQuery, ClientDto?>
{
    private readonly IClientRepository _clientRepository;

    public GetClientByIdQueryHandler(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<ClientDto?> Handle(GetClientByIdQuery query, CancellationToken cancellationToken = default)
    {
        var clientId = ClientId.From(query.ClientId);
        var client = await _clientRepository.GetByIdAsync(clientId);

        if (client == null)
            return null;

        return MapToDto(client);
    }

    private static ClientDto MapToDto(Domain.Clients.Aggregates.Client client)
    {
        return new ClientDto
        {
            Id = client.Id.Value,
            ClientName = client.ClientName,
            AllowedScopes = client.AllowedScopes.ToList(),
            AssociatedTenantIds = client.AssociatedTenantIds.ToList(),
            RequireClientSecret = client.RequireClientSecret,
            RequireConsent = client.RequireConsent,
            RequireMfa = client.RequireMfa,
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt
        };
    }
}

public class GetClientByNameQuery : IRequest<ClientDto?>
{
    public string ClientName { get; set; } = string.Empty;
}

public class GetClientByNameQueryHandler : IRequestHandler<GetClientByNameQuery, ClientDto?>
{
    private readonly IClientRepository _clientRepository;

    public GetClientByNameQueryHandler(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<ClientDto?> Handle(GetClientByNameQuery query, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByNameAsync(query.ClientName);

        if (client == null)
            return null;

        return MapToDto(client);
    }

    private static ClientDto MapToDto(Domain.Clients.Aggregates.Client client)
    {
        return new ClientDto
        {
            Id = client.Id.Value,
            ClientName = client.ClientName,
            AllowedScopes = client.AllowedScopes.ToList(),
            AssociatedTenantIds = client.AssociatedTenantIds.ToList(),
            RequireClientSecret = client.RequireClientSecret,
            RequireConsent = client.RequireConsent,
            RequireMfa = client.RequireMfa,
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt
        };
    }
}
