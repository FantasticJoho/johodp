namespace Johodp.Infrastructure.Persistence.DbContext;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Infrastructure.Persistence.Configurations;

public class JohodpDbContext : DbContext
{
    public JohodpDbContext(DbContextOptions<JohodpDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserTenant> UserTenants { get; set; } = null!;
    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Use 'dbo' schema (SQL Server convention) instead of default 'public'
        modelBuilder.HasDefaultSchema("dbo");

        // Ignore abstract base classes
        modelBuilder.Ignore<Johodp.Domain.Common.DomainEvent>();

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserTenantConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
    }
}
