namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Clients.Aggregates;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Domain.Clients.ValueObjects.ClientId id);
    Task<Client?> GetByNameAsync(string clientName);
    Task<Client?> GetByClientNameAsync(string clientName);
    Task<IEnumerable<Client>> GetAllAsync();
    Task<Client> AddAsync(Client client);
    Task<Client> UpdateAsync(Client client);
    Task<bool> DeleteAsync(Domain.Clients.ValueObjects.ClientId id);
}
