//using System.ComponentModel.DataAnnotations;
//using Microsoft.AspNetCore.Http;
//namespace CRUDAppUsingADO.Models.Bulk;

//public sealed class RecipientImportResult { public int Inserted { get; set; } public int Duplicates { get; set; } public int Invalid { get; set; } public List<string> Errors { get; set; } = []; }
//public sealed class RecipientRow { public string FullName { get; set; } = ""; public string? Email { get; set; } public string? Mobile { get; set; } public string? GroupName { get; set; } public string? Text1 { get; set; } public string? Text2 { get; set; } public string? Text3 { get; set; } public string? Text4 { get; set; } }
//public sealed class CardDesignSaveDto { [Required] public string DesignName { get; set; } = ""; [Required] public IFormFile Image { get; set; } = default!; [Range(1, 5000)] public int CanvasWidth { get; set; } = 1080; [Range(1, 5000)] public int CanvasHeight { get; set; } = 1080; [Required] public string CanvasJson { get; set; } = "{}"; }
//public sealed class BulkJobCreateDto { [Range(1, long.MaxValue)] public long CardDesignId { get; set; } public long? TemplateId { get; set; } [Required] public string OutputType { get; set; } = "Image"; [Required] public string Channels { get; set; } = "Both"; [Required] public List<long> RecipientIds { get; set; } = []; public Dictionary<string, string> ParameterValues { get; set; } = []; public DateTimeOffset? ScheduledAt { get; set; } }
//public sealed class RecipientStatusDto { public bool IsActive { get; set; } }

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CRUDAppUsingADO.Models.Bulk;

public sealed class RecipientRow
{
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string? GroupName { get; set; }

    public string? Text1 { get; set; }
    public string? Text2 { get; set; }
    public string? Text3 { get; set; }
    public string? Text4 { get; set; }
}

public sealed class RecipientImportResult
{
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Invalid { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class RecipientStatusDto
{
    public bool IsActive { get; set; }
}

public sealed class CardDesignSaveDto
{
    [Required]
    [StringLength(200)]
    public string DesignName { get; set; } = "";

    [Required]
    public string TemplateType { get; set; } = "Invitation";

    // Original uploaded background file format.
    [Required]
    public string UploadFormat { get; set; } = "PNG";

    // Final generated card format.
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

public sealed class BulkJobCreateDto
{
    [Range(1, long.MaxValue)]
    public long CardDesignId { get; set; }

    public long? TemplateId { get; set; }

    // Existing BulkJobs.OutputType: Image or PDF.
    [Required]
    public string OutputType { get; set; } = "Image";

    [Required]
    public string Channels { get; set; } = "Both";

    [Required]
    public List<long> RecipientIds { get; set; } = [];

    public Dictionary<string, string> ParameterValues { get; set; } = [];

    public DateTimeOffset? ScheduledAt { get; set; }
}
