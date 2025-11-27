namespace Johodp.Application.Common.Interfaces;

using Johodp.Domain.Common;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IClientRepository Clients { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
