namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Domain.Clients.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence.DbContext;

public class ClientRepository : IClientRepository
{
    private readonly JohodpDbContext _context;

    public ClientRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<Client?> GetByIdAsync(ClientId id)
    {
        return await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Client?> GetByNameAsync(string clientName)
    {
        return await _context.Clients
            .FirstOrDefaultAsync(c => c.ClientName == clientName);
    }

    public async Task<Client?> GetByClientNameAsync(string clientName)
    {
        return await _context.Clients
            .FirstOrDefaultAsync(c => c.ClientName == clientName);
    }

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        return await _context.Clients.ToListAsync();
    }

    public async Task<Client> AddAsync(Client client)
    {
        await _context.Clients.AddAsync(client);
        return client;
    }

    public async Task<Client> UpdateAsync(Client client)
    {
        _context.Clients.Update(client);
        return await Task.FromResult(client);
    }

    public async Task<bool> DeleteAsync(ClientId id)
    {
        var client = await GetByIdAsync(id);
        if (client == null)
            return false;

        _context.Clients.Remove(client);
        return true;
    }
}
