namespace Johodp.Infrastructure.Persistence.DbContext;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Infrastructure.Persistence.Configurations;

public class JohodpDbContext : DbContext
{
    public JohodpDbContext(DbContextOptions<JohodpDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<Scope> Scopes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new ScopeConfiguration());
    }
}
