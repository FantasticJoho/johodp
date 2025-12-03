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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Use 'dbo' schema (SQL Server convention) instead of default 'public'
        modelBuilder.HasDefaultSchema("dbo");

        // Ignore abstract base classes
        modelBuilder.Ignore<Johodp.Messaging.Events.DomainEvent>();

        // Ignore Value Objects (they are not entities)
        modelBuilder.Ignore<Johodp.Domain.Users.ValueObjects.UserId>();
        modelBuilder.Ignore<Johodp.Domain.Clients.ValueObjects.ClientId>();
        modelBuilder.Ignore<Johodp.Domain.Tenants.ValueObjects.TenantId>();
        modelBuilder.Ignore<Johodp.Domain.CustomConfigurations.ValueObjects.CustomConfigurationId>();

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new CustomConfigurationConfiguration());
    }
}
