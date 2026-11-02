using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models.DTOs;

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    //[Required, MinLength(8)]
    //public string NewPassword { get; set; } = string.Empty;

    //[Compare("NewPassword")]
    //public string ConfirmPassword { get; set; } = string.Empty;


    [Required]
    [MinLength(12)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}