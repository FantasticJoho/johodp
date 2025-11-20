namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

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

        // Multi-tenancy - store as JSONB array
        builder.Property<List<string>>("_tenantIds")
            .HasColumnName("TenantIds")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.ScopeId)
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? Johodp.Domain.Users.ValueObjects.ScopeId.From(v.Value) : null);

        // Relations
        builder.HasOne(x => x.Scope)
            .WithMany()
            .HasForeignKey("ScopeId")
            .IsRequired(false);

        builder.HasMany(x => x.Roles)
            .WithMany()
            .UsingEntity("UserRoles");

        builder.HasMany(x => x.Permissions)
            .WithMany()
            .UsingEntity("UserPermissions");

        // Ignore computed property and domain events
        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.DomainEvents);
    }
}
