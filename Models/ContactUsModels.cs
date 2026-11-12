
using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models;

public sealed class ContactUsCreateDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(25)]
    [RegularExpression(@"^[0-9+\-\s()]{7,25}$",
        ErrorMessage = "Enter a valid phone number.")]
    public string PhoneNumber { get; set; } = "";

    [StringLength(100)]
    public string? Subject { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Message { get; set; } = "";
}

public sealed class ContactUsMessageDto
{
    public long ContactId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string? Subject { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
