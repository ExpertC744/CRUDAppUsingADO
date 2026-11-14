using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CRUDAppUsingADO.Models
{
    public class EventRegistrationDto
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        public string RegistrationType { get; set; } = "Individual";

        public string? GroupName { get; set; }

        public string? TeamLeadName { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(30)]
        public List<EventParticipantDto> Participants { get; set; } = new();
    }

    public class EventParticipantDto
    {
        [Required, StringLength(150)]
        public string FullName { get; set; } = "";

        [Required]
        public string Gender { get; set; } = "";

        [Required]
        public DateTime DOB { get; set; }

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = "";

        [Required, Phone, StringLength(20)]
        public string WhatsAppNo { get; set; } = "";

        [Required, StringLength(500)]
        public string Address { get; set; } = "";

        public IFormFile? ProfileImage { get; set; }

        [Required]
        public string RegistrationCategory { get; set; } = "";

        // Student / Fresher fields
        public string? CollegeName { get; set; }

        public string? Stream { get; set; }

        public string? Branch { get; set; }

        public string? AcademicYear { get; set; }

        // Working Professional fields
        public string? OrganizationName { get; set; }

        public string? Designation { get; set; }

        public string? Experience { get; set; }
    }

    public class EventRegistrationResultDto
    {
        public int RegistrationId { get; set; }
        public int EventId { get; set; }
        public string RegistrationType { get; set; } = "";
        public int TotalParticipants { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
