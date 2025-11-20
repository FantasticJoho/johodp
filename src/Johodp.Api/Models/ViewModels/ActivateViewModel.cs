namespace Johodp.Api.Models.ViewModels;

using System.ComponentModel.DataAnnotations;

public class ActivateViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string TenantId { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string MaskedEmail { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation est requise")]
    [Compare("NewPassword", ErrorMessage = "Les mots de passe ne correspondent pas")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ActivateSuccessViewModel
{
    public string ReturnUrl { get; set; } = string.Empty;
}
