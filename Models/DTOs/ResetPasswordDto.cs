using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models.DTOs;

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}