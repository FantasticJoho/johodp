namespace Johodp.Application.Clients.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Application.Clients.DTOs;
using Johodp.Domain.Clients.ValueObjects;

public class UpdateClientCommand : IRequest<Result<ClientDto>>
{
    public Guid ClientId { get; set; }
    public UpdateClientDto Data { get; set; } = null!;
}

public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand, Result<ClientDto>>
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

    public async Task<Result<ClientDto>> Handle(UpdateClientCommand command, CancellationToken cancellationToken = default)
    {
        var clientId = ClientId.From(command.ClientId);
        var client = await _clientRepository.GetByIdAsync(clientId);

        if (client == null)
        {
            return Result<ClientDto>.Failure(Error.NotFound(
                "CLIENT_NOT_FOUND",
                $"Client with ID '{command.ClientId}' not found"));
        }

        var dto = command.Data;

        // Update allowed scopes
        if (dto.AllowedScopes != null)
        {
            client.UpdateAllowedScopes(dto.AllowedScopes.ToArray());
        }

        // Update associated tenants (replace all)
        if (dto.AssociatedTenantIds != null)
        {
            // Remove tenants that are no longer associated
            var currentTenants = client.AssociatedTenantIds.ToList();
            var tenantsToRemove = currentTenants.Except(dto.AssociatedTenantIds).ToList();
            foreach (var tenantId in tenantsToRemove)
            {
                client.DissociateTenant(tenantId);
            }

            // Add new tenant associations
            var tenantsToAdd = dto.AssociatedTenantIds.Except(currentTenants).ToList();
            foreach (var tenantId in tenantsToAdd)
            {
                client.AssociateTenant(tenantId);
            }
        }

        // Update active status
        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
                client.Activate();
            else
                client.Deactivate();
        }

        // Update MFA requirement
        if (dto.RequireMfa.HasValue)
        {
            if (dto.RequireMfa.Value)
                client.EnableMfa();
            else
                client.DisableMfa();
        }

        // Update client secret requirement
        if (dto.RequireClientSecret.HasValue)
        {
            if (dto.RequireClientSecret.Value)
                client.RequireSecret();
            else
                client.AllowPublicClient();
        }

        // Update consent requirement
        if (dto.RequireConsent.HasValue)
        {
            if (dto.RequireConsent.Value)
                client.EnableConsent();
            else
                client.DisableConsent();
        }

        // Save client
        await _clientRepository.UpdateAsync(client);
        await _unitOfWork.SaveChangesAsync();

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
