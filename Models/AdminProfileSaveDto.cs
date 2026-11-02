using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models;

public class AdminProfileSaveDto
{
    [Required, StringLength(200)]
    public string OrganizationName { get; set; } = "";

    [Required, StringLength(150)]
    public string Designation { get; set; } = "";

    [Required, StringLength(150)]
    public string Department { get; set; } = "";

    [Required, StringLength(100)]
    public string City { get; set; } = "";

    public IFormFile? ProfileImage { get; set; }
}