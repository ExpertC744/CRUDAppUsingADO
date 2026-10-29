using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models
{
    public class FeedbackDto
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [Required]
        [StringLength(240, MinimumLength = 1)]
        public string Description { get; set; } = string.Empty;

        public bool nBit { get; set; }
    }

    public class FeedbackUserDto
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
    }

    // Used to display feedback retrieved from the database
    public class FeedbackViewModel
    {
        public int FeedbackId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool nBit { get; set; }
    }
}
