namespace Johodp.Application.Clients.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Clients.DTOs;
using Johodp.Domain.Clients.ValueObjects;

public class UpdateClientCommand
{
    public Guid ClientId { get; set; }
    public UpdateClientDto Data { get; set; } = null!;
}

public class UpdateClientCommandHandler
{
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateClientCommandHandler(
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork)
    {
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ClientDto> Handle(UpdateClientCommand command)
    {
        var clientId = ClientId.From(command.ClientId);
        var client = await _clientRepository.GetByIdAsync(clientId);

        if (client == null)
        {
            throw new InvalidOperationException($"Client with ID '{command.ClientId}' not found");
        }

        var dto = command.Data;

        // Update allowed scopes
        if (dto.AllowedScopes != null)
        {
            client.UpdateAllowedScopes(dto.AllowedScopes.ToArray());
        }

        // Update redirect URIs (replace all)
        if (dto.AllowedRedirectUris != null)
        {
            // Remove all existing
            var currentUris = client.AllowedRedirectUris.ToList();
            foreach (var uri in currentUris)
            {
                client.RemoveRedirectUri(uri);
            }

            // Add new ones
            foreach (var uri in dto.AllowedRedirectUris)
            {
                client.AddRedirectUri(uri);
            }
        }

        // Update CORS origins
        if (dto.AllowedCorsOrigins != null)
        {
            client.UpdateCorsOrigins(dto.AllowedCorsOrigins.ToArray());
        }

        // Update active status
        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
                client.Activate();
            else
                client.Deactivate();
        }

        // Save client
        await _clientRepository.UpdateAsync(client);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(client);
    }

    private static ClientDto MapToDto(Domain.Clients.Aggregates.Client client)
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
