namespace Johodp.Application.Clients.Queries;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Application.Common.Handlers;
using Johodp.Contracts.Clients;
using Johodp.Domain.Clients.ValueObjects;
using Microsoft.Extensions.Logging;

public class GetClientByIdQuery : IRequest<Result<ClientDto>>
{
    public Guid ClientId { get; set; }
}

public class GetClientByIdQueryHandler : BaseHandler<GetClientByIdQuery, Result<ClientDto>>
{
    private readonly IClientRepository _clientRepository;

    public GetClientByIdQueryHandler(
        IClientRepository clientRepository,
        ILogger<GetClientByIdQueryHandler> logger) : base(logger)
    {
        _clientRepository = clientRepository;
    }

    protected override async Task<Result<ClientDto>> HandleCore(GetClientByIdQuery query, CancellationToken cancellationToken)
    {
        var clientId = ClientId.From(query.ClientId);
        var client = await _clientRepository.GetByIdAsync(clientId);

        if (client == null)
        {
            return Result<ClientDto>.Failure(ClientErrors.NotFound(query.ClientId));
        }

        return Result<ClientDto>.Success(MapToDto(client));
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

public class GetClientByNameQuery : IRequest<Result<ClientDto>>
{
    public string ClientName { get; set; } = string.Empty;
}

public class GetClientByNameQueryHandler : BaseHandler<GetClientByNameQuery, Result<ClientDto>>
{
    private readonly IClientRepository _clientRepository;

    public GetClientByNameQueryHandler(
        IClientRepository clientRepository,
        ILogger<GetClientByNameQueryHandler> logger) : base(logger)
    {
        _clientRepository = clientRepository;
    }

    protected override async Task<Result<ClientDto>> HandleCore(GetClientByNameQuery query, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByNameAsync(query.ClientName);

        if (client == null)
        {
            return Result<ClientDto>.Failure(ClientErrors.NotFoundByName(query.ClientName));
        }

        return Result<ClientDto>.Success(MapToDto(client));
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
