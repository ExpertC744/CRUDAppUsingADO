using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models.DTOs
{
    public sealed class CardDesignSaveDto
    {
        [Required]
        public string DesignName { get; set; } = "";

        [Required]
        public string TemplateType { get; set; } = "Invitation";

        [Required]
        public string UploadFormat { get; set; } = "PNG";

        [Required]
        public string OutputFormat { get; set; } = "PNG";

        [Required]
        public IFormFile Image { get; set; } = default!;

        [Range(1, 5000)]
        public int CanvasWidth { get; set; } = 1080;

        [Range(1, 5000)]
        public int CanvasHeight { get; set; } = 1080;

        [Required]
        public string CanvasJson { get; set; } = "{}";
    }
}
