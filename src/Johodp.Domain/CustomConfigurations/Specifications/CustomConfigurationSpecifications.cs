namespace Johodp.Domain.CustomConfigurations.Specifications;

using Johodp.Domain.Common.Specifications;
using Johodp.Domain.CustomConfigurations.Aggregates;

/// <summary>
/// Specification for active custom configurations
/// </summary>
public class ActiveCustomConfigSpecification : Specification<CustomConfiguration>
{
    public ActiveCustomConfigSpecification()
    {
        Criteria = config => config.IsActive;
    }
}

/// <summary>
/// Specification for custom configurations supporting a specific language
/// </summary>
public class CustomConfigByLanguageSpecification : Specification<CustomConfiguration>
{
    public CustomConfigByLanguageSpecification(string languageCode)
    {
        Criteria = config => config.SupportedLanguages.Contains(languageCode);
    }
}

/// <summary>
/// Specification for custom configurations with branding defined
/// </summary>
public class CustomConfigWithBrandingSpecification : Specification<CustomConfiguration>
{
    public CustomConfigWithBrandingSpecification()
    {
        Criteria = config => 
            config.PrimaryColor != null || 
            config.SecondaryColor != null || 
            config.LogoUrl != null || 
            config.BackgroundImageUrl != null ||
            config.CustomCss != null;
    }
}

/// <summary>
/// Specification for custom configurations by name search
/// </summary>
public class CustomConfigByNameSearchSpecification : Specification<CustomConfiguration>
{
    public CustomConfigByNameSearchSpecification(string searchTerm)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();
        Criteria = config => config.Name.ToLower().Contains(lowerSearch);
    }
}

/// <summary>
/// Example usage patterns for CustomConfiguration specifications
/// </summary>
public static class CustomConfigSpecificationExamples
{
    /// <summary>
    /// Get active configs with branding
    /// </summary>
    public static Specification<CustomConfiguration> ActiveConfigsWithBranding()
    {
        return new ActiveCustomConfigSpecification()
            .And(new CustomConfigWithBrandingSpecification());
    }

    /// <summary>
    /// Get active configs supporting a specific language
    /// </summary>
    public static Specification<CustomConfiguration> ActiveConfigsByLanguage(string languageCode)
    {
        return new ActiveCustomConfigSpecification()
            .And(new CustomConfigByLanguageSpecification(languageCode));
    }

    /// <summary>
    /// Search active configs by name
    /// </summary>
    public static Specification<CustomConfiguration> SearchActiveConfigs(string searchTerm)
    {
        return new ActiveCustomConfigSpecification()
            .And(new CustomConfigByNameSearchSpecification(searchTerm));
    }

    /// <summary>
    /// Get configs without branding (need setup)
    /// </summary>
    public static Specification<CustomConfiguration> ConfigsNeedingBranding()
    {
        return new ActiveCustomConfigSpecification()
            .And(new CustomConfigWithBrandingSpecification().Not());
    }

    /// <summary>
    /// Get configs supporting multiple languages
    /// </summary>
    public static Specification<CustomConfiguration> MultilingualConfigs()
    {
        return new MultilingualCustomConfigSpecification();
    }
}

/// <summary>
/// Specification for multilingual configurations
/// </summary>
public class MultilingualCustomConfigSpecification : Specification<CustomConfiguration>
{
    public MultilingualCustomConfigSpecification()
    {
        Criteria = config => config.SupportedLanguages.Count() > 1;
    }
}
