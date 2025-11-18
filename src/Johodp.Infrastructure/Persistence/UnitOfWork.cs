namespace Johodp.Infrastructure.Persistence;

using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence.DbContext;
using Johodp.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly JohodpDbContext _context;
    private IUserRepository? _userRepository;
    private IClientRepository? _clientRepository;
    private IRoleRepository? _roleRepository;
    private IPermissionRepository? _permissionRepository;
    private IScopeRepository? _scopeRepository;

    public UnitOfWork(JohodpDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _userRepository ??= new UserRepository(_context);
    public IClientRepository Clients => _clientRepository ??= new ClientRepository(_context);
    public IRoleRepository Roles => _roleRepository ??= new RoleRepository(_context);
    public IPermissionRepository Permissions => _permissionRepository ??= new PermissionRepository(_context);
    public IScopeRepository Scopes => _scopeRepository ??= new ScopeRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.RollbackTransactionAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
