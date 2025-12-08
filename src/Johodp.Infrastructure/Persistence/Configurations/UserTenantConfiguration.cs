using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Users.Entities;

namespace Johodp.Infrastructure.Persistence.Configurations;

public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("UserTenants", "dbo");
        builder.HasKey(ut => new { ut.UserId, ut.TenantId });

        builder.Property(ut => ut.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ut => ut.AssignedAt)
            .IsRequired();

        builder.HasOne(ut => ut.User)
            .WithMany()
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ut => ut.Tenant)
            .WithMany()
            .HasForeignKey(ut => ut.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure SubScopes as JSON column (null-safe, no nullable warnings)
        // Utilise JsonSerializerOptions.Default pour éviter les conversions nullables
        // Le ! après Deserialize indique à l'analyseur que le résultat ne sera jamais null (car géré juste avant)
        builder.Property(ut => ut.SubScopes)
            .HasConversion(
                v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, System.Text.Json.JsonSerializerOptions.Default),
                v => v == null ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, System.Text.Json.JsonSerializerOptions.Default)!
            )
            .HasColumnType("jsonb");

        // Comparaison de valeur custom pour les listes (null-safe)
        builder.Property(ut => ut.SubScopes)
            .Metadata.SetValueComparer(
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => System.Linq.Enumerable.SequenceEqual(c1 ?? new List<string>(), c2 ?? new List<string>()),
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? new List<string>() : c.ToList()
                )
            );
    }
}
