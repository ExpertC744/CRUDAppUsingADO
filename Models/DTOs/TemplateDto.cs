using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models
{
    public class TemplateDto : IValidatableObject
    {
        [Range(1, 2)]
        public int TemplateType { get; set; }

        [Range(1, 6)]
        public int SendType { get; set; }

        [Required, StringLength(200)]
        public string TemplateName { get; set; } = "";

        [Required]
        public string Message { get; set; } = "";

        public bool WithUserName { get; set; }

        [Range(0, 100)]
        public int? NoOfParameters { get; set; }

        [Range(1, int.MaxValue)]
        public int TemplateAdminId { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (TemplateType != 1 && TemplateType != 2)
            {
                yield return new ValidationResult(
                    "Template Type must be 1 (Utility) or 2 (Marketing).",
                    new[] { nameof(TemplateType) });
            }

            if (SendType < 1 || SendType > 6)
            {
                yield return new ValidationResult(
                    "Invalid Send Type.",
                    new[] { nameof(SendType) });
            }

            if (NoOfParameters < 0)
            {
                yield return new ValidationResult(
                    "Number of parameters cannot be negative.",
                    new[] { nameof(NoOfParameters) });
            }
        }
    }
}