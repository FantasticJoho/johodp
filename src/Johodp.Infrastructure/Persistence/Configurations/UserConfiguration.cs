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


        // Multi-tenant: Role, Scope, and TenantId are now managed in UserTenant entity.


        // Unique index on Email (if required, otherwise handle uniqueness in UserTenant)

        // Ignore computed property and domain events
        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.DomainEvents);
    }
}
