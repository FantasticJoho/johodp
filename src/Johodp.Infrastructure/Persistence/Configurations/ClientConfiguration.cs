namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Domain.Clients.ValueObjects;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                v => v.Value,
                v => ClientId.From(v))
            .ValueGeneratedNever();

        builder.Property(x => x.ClientName)
            .HasMaxLength(100)
            .IsRequired();

        var allowedScopesProperty = builder.Property(x => x.AllowedScopes)
            .HasConversion(
                v => string.Join(";", v),
                v => v.Split(";", StringSplitOptions.RemoveEmptyEntries));
        
        allowedScopesProperty.Metadata.SetValueComparer(
            new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<string[]>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToArray()));

        // AssociatedTenantIds - liste des tenants associés au client
        var associatedTenantsProperty = builder.Property<List<string>>("_associatedTenantIds")
            .HasColumnName("associated_tenant_ids")
            .HasConversion(
                v => string.Join(";", v),
                v => v.Split(";", StringSplitOptions.RemoveEmptyEntries).ToList())
            .IsRequired();
        
        associatedTenantsProperty.Metadata.SetValueComparer(
            new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(x => x.RequireClientSecret)
            .HasDefaultValue(true);

        builder.Property(x => x.RequireConsent)
            .HasDefaultValue(true);

        builder.Property(x => x.RequireMfa)
            .HasDefaultValue(false);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Ignore(x => x.DomainEvents);
    }
}
