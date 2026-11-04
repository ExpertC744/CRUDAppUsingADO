using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models
{
    public class TemplateAdminDto
    {
        public Guid? UserId { get; set; }

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = "";

        [Required, Phone, StringLength(20)]
        public string Mobile { get; set; } = "";

        [StringLength(500)]
        public string? Details { get; set; }
    }
}