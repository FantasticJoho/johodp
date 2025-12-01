namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.CustomConfigurations.Aggregates;
using Johodp.Domain.CustomConfigurations.ValueObjects;
using Johodp.Infrastructure.Persistence.DbContext;

public class CustomConfigurationRepository : ICustomConfigurationRepository
{
    private readonly JohodpDbContext _context;

    public CustomConfigurationRepository(JohodpDbContext context)
    {
        _context = context;
    }

    public async Task<CustomConfiguration?> GetByIdAsync(CustomConfigurationId id)
    {
        return await _context.CustomConfigurations.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CustomConfiguration?> GetByNameAsync(string name)
    {
        return await _context.CustomConfigurations.FirstOrDefaultAsync(c => c.Name == name);
    }

    public async Task<CustomConfiguration> AddAsync(CustomConfiguration customConfiguration)
    {
        await _context.CustomConfigurations.AddAsync(customConfiguration);
        return customConfiguration;
    }

    public Task<CustomConfiguration> UpdateAsync(CustomConfiguration customConfiguration)
    {
        _context.CustomConfigurations.Update(customConfiguration);
        return Task.FromResult(customConfiguration);
    }

    public async Task<bool> DeleteAsync(CustomConfigurationId id)
    {
        var customConfig = await GetByIdAsync(id);
        if (customConfig == null)
            return false;

        _context.CustomConfigurations.Remove(customConfig);
        return true;
    }

    public async Task<IEnumerable<CustomConfiguration>> GetAllAsync()
    {
        return await _context.CustomConfigurations.ToListAsync();
    }

    public async Task<IEnumerable<CustomConfiguration>> GetActiveAsync()
    {
        return await _context.CustomConfigurations.Where(c => c.IsActive).ToListAsync();
    }
}
