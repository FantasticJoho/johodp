namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("UserTenants");

        // Composite primary key
        builder.HasKey(ut => new { ut.UserId, ut.TenantId });

        // UserId configuration
        builder.Property(ut => ut.UserId)
            .HasConversion(
                v => v.Value,
                v => UserId.From(v))
            .IsRequired();

        // TenantId configuration
        builder.Property(ut => ut.TenantId)
            .HasConversion(
                v => v.Value,
                v => TenantId.From(v))
            .IsRequired();

        // Role - string provided by external application
        builder.Property(ut => ut.Role)
            .HasMaxLength(100)
            .IsRequired();

        // Scope - string provided by external application
        builder.Property(ut => ut.Scope)
            .HasMaxLength(200)
            .IsRequired();

        // Timestamps
        builder.Property(ut => ut.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(ut => ut.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // Indexes for common queries
        builder.HasIndex(ut => ut.TenantId)
            .HasDatabaseName("IX_UserTenants_TenantId");

        builder.HasIndex(ut => ut.UserId)
            .HasDatabaseName("IX_UserTenants_UserId");
    }
}
