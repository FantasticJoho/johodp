namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Users.Aggregates;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Domain.Users.ValueObjects.UserId id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> AddAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> DeleteAsync(Domain.Users.ValueObjects.UserId id);
}
