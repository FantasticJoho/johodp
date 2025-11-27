namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Tenants.ValueObjects;

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

        // Branding
        builder.Property(t => t.PrimaryColor)
            .HasMaxLength(50);

        builder.Property(t => t.SecondaryColor)
            .HasMaxLength(50);

        builder.Property(t => t.LogoUrl)
            .HasMaxLength(500);

        builder.Property(t => t.BackgroundImageUrl)
            .HasMaxLength(500);

        builder.Property(t => t.CustomCss)
            .HasColumnType("text");

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

        // Localization
        builder.Property(t => t.DefaultLanguage)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("fr-FR");

        builder.Property(t => t.Timezone)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Europe/Paris");

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("EUR");

        // Collections stored as JSON
        builder.Property<List<string>>("_urls")
            .HasColumnName("Urls")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property<List<string>>("_supportedLanguages")
            .HasColumnName("SupportedLanguages")
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
            .HasMaxLength(200)
            .IsRequired(false);
    }
}
