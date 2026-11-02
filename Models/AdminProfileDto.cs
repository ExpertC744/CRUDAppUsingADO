using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models;

public class AdminProfileDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    [Required]
    public string OrganizationName { get; set; } = "";

    [Required]
    public string Designation { get; set; } = "";

    [Required]
    public string Department { get; set; } = "";

    [Required]
    public string City { get; set; } = "";

    public string? ProfileImageUrl { get; set; }

    public bool nBit { get; set; }
    public bool IsProfileCreated { get; set; }
}