namespace Johodp.Application.Clients.Queries;

using Johodp.Application.Clients.DTOs;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;


public class GetAllClientsQueryHandler1
{
    private readonly IClientRepository _clientRepository;

    public GetAllClientsQueryHandler1(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public Task<IEnumerable<ClientDto>> Handle(string request, CancellationToken cancellationToken)
    {
        // Pour l'instant, utilisons une approche simple - dans un vrai projet, 
        // il faudrait ajouter GetAllAsync au repository ou utiliser un QueryService
        throw new NotImplementedException("GetAllClients query not yet implemented. Add GetAllAsync to IClientRepository.");
    }
}
