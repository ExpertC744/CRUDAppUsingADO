using CRUDAppUsingADO.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRUDAppUsingADO.Controllers;

[ApiController]
[Route("api/admin/whatsapp-templates")]
[Authorize(Roles = "Admin")]
public sealed class WhatsAppTemplateSelectionController(
    WhatsAppTemplateSelectionDAL dal) : ControllerBase
{
    // GET api/admin/whatsapp-templates?templateType=1&sendType=2
    [HttpGet]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] int? templateType,
        [FromQuery] int? sendType,
        CancellationToken ct)
    {
        if (templateType.HasValue &&
            templateType.Value is not (1 or 2))
        {
            return BadRequest(
                "TemplateType must be 1 (Utility) or 2 (Marketing).");
        }

        if (sendType.HasValue &&
            sendType.Value is not (1 or 2 or 3 or 4))
        {
            return BadRequest(
                "SendType must be between 1 and 4.");
        }

        var templates = await dal.GetTemplatesAsync(
            templateType,
            sendType,
            ct);

        return Ok(templates);
    }

    // GET api/admin/whatsapp-templates/10
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetTemplate(
        long id,
        CancellationToken ct)
    {
        var template = await dal.GetTemplateByIdAsync(id, ct);

        return template == null
            ? NotFound(new { message = "Active template not found." })
            : Ok(template);
    }
}