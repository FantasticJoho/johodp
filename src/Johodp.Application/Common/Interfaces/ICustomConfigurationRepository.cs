namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.CustomConfigurations.Aggregates;
using Johodp.Domain.CustomConfigurations.ValueObjects;

public interface ICustomConfigurationRepository
{
    Task<CustomConfiguration?> GetByIdAsync(CustomConfigurationId id);
    Task<CustomConfiguration?> GetByNameAsync(string name);
    Task<CustomConfiguration> AddAsync(CustomConfiguration customConfiguration);
    Task<CustomConfiguration> UpdateAsync(CustomConfiguration customConfiguration);
    Task<bool> DeleteAsync(CustomConfigurationId id);
    Task<IEnumerable<CustomConfiguration>> GetAllAsync();
    Task<IEnumerable<CustomConfiguration>> GetActiveAsync();
}
