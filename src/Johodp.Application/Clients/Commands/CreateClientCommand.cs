namespace Johodp.Application.Clients.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Application.Common.Handlers;
using Johodp.Contracts.Clients;
using Johodp.Domain.Clients.Aggregates;
using Microsoft.Extensions.Logging;

public class CreateClientCommand : IRequest<Result<ClientDto>>
{
    public CreateClientDto Data { get; set; } = null!;
}

public class CreateClientCommandHandler : BaseHandler<CreateClientCommand, Result<ClientDto>>
{
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateClientCommandHandler(
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateClientCommandHandler> logger) : base(logger)
    {
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<Result<ClientDto>> HandleCore(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Data;

        // Check if client name already exists
        var existingClient = await _clientRepository.GetByNameAsync(dto.ClientName);
        if (existingClient != null)
        {
            return Result<ClientDto>.Failure(Error.Conflict(
                "CLIENT_ALREADY_EXISTS",
                $"A client with name '{dto.ClientName}' already exists"));
        }

        // Create client aggregate
        var client = Client.Create(
            dto.ClientName,
            dto.AllowedScopes?.ToArray() ?? Array.Empty<string>(),
            dto.RequireConsent,
            dto.RequireMfa);

        // Save client
        await _clientRepository.AddAsync(client);
        await _unitOfWork.SaveChangesAsync();

        return Result<ClientDto>.Success(MapToDto(client));
    }

    private static ClientDto MapToDto(Client client)
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
