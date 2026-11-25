using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models
{
    public class EventFeedbackSubmitDto
    {
        [Required]
        public int EventId { get; set; }

        [Required, StringLength(150)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = "";

        [Required, StringLength(20)]
        public string Mobile { get; set; } = "";

        [Required, StringLength(1000)]
        public string Feedback { get; set; } = "";
    }

    public class EventFeedbackApprovalDto
    {
        [Required]
        public bool IsApproved { get; set; }
    }
}