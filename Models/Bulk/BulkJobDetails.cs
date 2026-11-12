namespace CRUDAppUsingADO.Models.Bulk;

public sealed class BulkJobDetails
{
    public Guid JobId { get; set; }
    public Guid AdminUserId { get; set; }

    public long CardDesignId { get; set; }
    public int? TemplateId { get; set; }

    public string OutputType { get; set; } = "Image";
    public string OutputFormat { get; set; } = "PNG";
    public string Channels { get; set; } = "WhatsApp";

    public string ParameterValuesJson { get; set; } = "{}";

    public string ImagePath { get; set; } = "";
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public string CanvasJson { get; set; } = "{}";

    public string? TemplateName { get; set; }
    public string? TemplateMessage { get; set; }
    public int TemplateParameterCount { get; set; }
    public int TemplateSendType { get; set; } = 1;
}

public sealed class BulkRecipientDetails
{
    public long RecipientId { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Mobile { get; set; }

    public string? Text1 { get; set; }
    public string? Text2 { get; set; }
    public string? Text3 { get; set; }
    public string? Text4 { get; set; }
}