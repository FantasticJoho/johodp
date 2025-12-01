namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Tenants.ValueObjects;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                v => v.Value,
                v => UserId.From(v))
            .ValueGeneratedNever();

        builder.Property(x => x.Email)
            .HasConversion(
                v => v.Value,
                v => Email.Create(v))
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.LastName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EmailConfirmed)
            .HasDefaultValue(false);

        builder.Property(x => x.Status)
            .HasConversion(
                v => v.Value,
                v => UserStatus.FromValue<UserStatus>(v))
            .IsRequired();

        builder.Property(x => x.ActivatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.MFAEnabled)
            .HasDefaultValue(false);

        // Single tenant with role and scope
        builder.Property(x => x.TenantId)
            .HasConversion(
                v => v.Value,
                v => TenantId.From(v))
            .IsRequired();

        builder.Property(x => x.Role)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Scope)
            .HasMaxLength(200)
            .IsRequired();

        // Foreign key to Tenant
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite unique index on (Email, TenantId)
        builder.HasIndex(x => new { x.Email, x.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_users_Email_TenantId");

        // Index on TenantId for queries
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_users_TenantId");

        // Ignore computed property and domain events
        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.DomainEvents);
    }
}
