namespace CRUDAppUsingADO.Models.Bulk;

public sealed class WhatsAppTemplateDto
{
    public long TemplateId { get; set; }

    public string TemplateName { get; set; } = "";

    public int TemplateType { get; set; }

    public string TemplateTypeName { get; set; } = "";

    public int SendType { get; set; }

    public string SendTypeName { get; set; } = "";

    public string Message { get; set; } = "";

    public bool WithUserName { get; set; }

    public int NoOfParameters { get; set; }
}