namespace Johodp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Johodp.Domain.CustomConfigurations.Aggregates;
using Johodp.Domain.CustomConfigurations.ValueObjects;

public class CustomConfigurationConfiguration : IEntityTypeConfiguration<CustomConfiguration>
{
    public void Configure(EntityTypeBuilder<CustomConfiguration> builder)
    {
        builder.ToTable("custom_configurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => CustomConfigurationId.From(value))
            .HasColumnName("id");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.Property(c => c.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        // Branding
        builder.Property(c => c.PrimaryColor)
            .HasMaxLength(50)
            .HasColumnName("primary_color");

        builder.Property(c => c.SecondaryColor)
            .HasMaxLength(50)
            .HasColumnName("secondary_color");

        builder.Property(c => c.LogoUrl)
            .HasMaxLength(500)
            .HasColumnName("logo_url");

        builder.Property(c => c.BackgroundImageUrl)
            .HasMaxLength(500)
            .HasColumnName("background_image_url");

        builder.Property(c => c.CustomCss)
            .HasColumnName("custom_css");

        // Localization - simple list of language codes stored as semicolon-separated string
        builder.Property<List<string>>("_supportedLanguages")
            .HasColumnName("supported_languages")
            .HasConversion(
                languages => string.Join(";", languages),
                value => string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(c => c.DefaultLanguage)
            .IsRequired()
            .HasMaxLength(10)
            .HasColumnName("default_language");
    }
}
