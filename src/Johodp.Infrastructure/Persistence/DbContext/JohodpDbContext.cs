namespace Johodp.Infrastructure.Persistence.DbContext;

using Microsoft.EntityFrameworkCore;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.CustomConfigurations.Aggregates;
using Johodp.Infrastructure.Persistence.Configurations;

public class JohodpDbContext : DbContext
{
    public JohodpDbContext(DbContextOptions<JohodpDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<CustomConfiguration> CustomConfigurations { get; set; } = null!;
    public DbSet<Johodp.Domain.Users.Entities.UserTenant> UserTenants { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Use 'dbo' schema (SQL Server convention) instead of default 'public'
        modelBuilder.HasDefaultSchema("dbo");

        // Ignore abstract base classes
        modelBuilder.Ignore<Johodp.Messaging.Events.DomainEvent>();

        // Value Objects are mapped via conversions in individual entity configurations
        // (Do NOT ignore the value object types globally; they are used by entity properties)

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new CustomConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new UserTenantConfiguration());
    }
}
