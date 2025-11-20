namespace Johodp.Application.Clients.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Clients.DTOs;
using Johodp.Domain.Clients.Aggregates;

public class CreateClientCommand
{
    public CreateClientDto Data { get; set; } = null!;
}

public class CreateClientCommandHandler
{
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateClientCommandHandler(
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork)
    {
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ClientDto> Handle(CreateClientCommand command)
    {
        var dto = command.Data;

        // Check if client name already exists
        var existingClient = await _clientRepository.GetByNameAsync(dto.ClientName);
        if (existingClient != null)
        {
            throw new InvalidOperationException($"A client with name '{dto.ClientName}' already exists");
        }

        // Validate at least one redirect URI
        if (dto.AllowedRedirectUris == null || dto.AllowedRedirectUris.Count == 0)
        {
            throw new ArgumentException("At least one redirect URI is required", nameof(dto.AllowedRedirectUris));
        }

        // Create client aggregate
        var client = Client.Create(
            dto.ClientName,
            dto.AllowedScopes?.ToArray() ?? Array.Empty<string>(),
            dto.AllowedRedirectUris.ToArray(),
            dto.AllowedCorsOrigins?.ToArray() ?? Array.Empty<string>(),
            dto.RequireConsent);

        // Save client
        await _clientRepository.AddAsync(client);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(client);
    }

    private static ClientDto MapToDto(Client client)
    {
        return new ClientDto
        {
            Id = client.Id.Value,
            ClientName = client.ClientName,
            AllowedScopes = client.AllowedScopes.ToList(),
            AllowedRedirectUris = client.AllowedRedirectUris.ToList(),
            AllowedCorsOrigins = client.AllowedCorsOrigins.ToList(),
            RequireClientSecret = client.RequireClientSecret,
            RequireConsent = client.RequireConsent,
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt
        };
    }
}
