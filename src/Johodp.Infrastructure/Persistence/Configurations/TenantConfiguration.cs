namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Domain.Clients.ValueObjects;
using Johodp.Domain.CustomConfigurations.ValueObjects;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => TenantId.From(value))
            .IsRequired();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt);

        // CustomConfiguration reference (required)
        builder.Property(t => t.CustomConfigurationId)
            .HasConversion(
                id => id.Value,
                value => CustomConfigurationId.From(value))
            .HasColumnName("custom_configuration_id")
            .IsRequired();

        // Notification configuration
        builder.Property(t => t.NotificationUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(t => t.ApiKey)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(t => t.NotifyOnAccountRequest)
            .IsRequired()
            .HasDefaultValue(false);

        // Collections stored as JSON
        builder.Property<List<string>>("_urls")
            .HasColumnName("Urls")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property<List<string>>("_allowedReturnUrls")
            .HasColumnName("AllowedReturnUrls")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property<List<string>>("_allowedCorsOrigins")
            .HasColumnName("AllowedCorsOrigins")
            .HasColumnType("jsonb")
            .IsRequired();

        // Associated client (a tenant can only have one client)
        builder.Property(t => t.ClientId)
            .HasConversion(
                v => v != null ? v.Value.ToString() : null,
                v => v != null ? ClientId.From(Guid.Parse(v)) : null)
            .HasMaxLength(200)
            .IsRequired(false);
    }
}
