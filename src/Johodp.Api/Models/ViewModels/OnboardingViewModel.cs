namespace Johodp.Api.Models.ViewModels;

using System.ComponentModel.DataAnnotations;

public class OnboardingViewModel
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? ReturnUrl { get; set; }

    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le prénom est requis")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
}

public class OnboardingPendingViewModel
{
    public string Email { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
}
