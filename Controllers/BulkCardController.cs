using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models.Bulk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class BulkCardController(
    BulkCardDAL dal,
    IWebHostEnvironment env) : ControllerBase
{
    private bool AdminId(out Guid id)
    {
        var c = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");

        return Guid.TryParse(c?.Value, out id);
    }

    // SAVE CARD DESIGN
    [HttpPost("cards")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> SaveCard(
        [FromForm] CardDesignSaveDto dto,
        CancellationToken ct)
    {
        if (!AdminId(out var admin))
            return Unauthorized();

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (dto.Image == null ||
            dto.Image.Length == 0 ||
            dto.Image.Length > 10_000_000)
            return BadRequest("Image is required and must be under 10 MB.");

        var ext = Path.GetExtension(dto.Image.FileName).ToLowerInvariant();

        if (ext is not (".png" or ".jpg" or ".jpeg"))
            return BadRequest("Only PNG/JPG images are accepted.");

        var dir = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
            "uploads",
            "card-designs");

        Directory.CreateDirectory(dir);

        var file = Guid.NewGuid().ToString("N") + ext;
        var path = Path.Combine(dir, file);

        await using (var fs = System.IO.File.Create(path))
        {
            await dto.Image.CopyToAsync(fs, ct);
        }

        try
        {
            var id = await dal.SaveDesignAsync(
                admin,
                dto.DesignName,
                $"/uploads/card-designs/{file}",
                dto.CanvasWidth,
                dto.CanvasHeight,
                dto.CanvasJson,
                ct);

            return Ok(new
            {
                cardDesignId = id,
                imageUrl = $"/uploads/card-designs/{file}"
            });
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }
    }

    // CREATE BULK JOB
    [HttpPost("bulk-jobs")]
    public async Task<IActionResult> CreateJob(
        [FromBody] BulkJobCreateDto dto,
        CancellationToken ct)
    {
        if (!AdminId(out var admin))
            return Unauthorized();

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (dto.OutputType is not ("Image" or "PDF"))
            return BadRequest("OutputType must be Image or PDF.");

        if (dto.Channels is not ("Email" or "WhatsApp" or "Both"))
            return BadRequest("Invalid Channels.");

        try
        {
            var id = await dal.CreateJobAsync(admin, dto, ct);

            return Accepted(new
            {
                jobId = id,
                status = dto.ScheduledAt > DateTimeOffset.UtcNow
                    ? "Scheduled"
                    : "Queued",
                statusUrl = $"/api/admin/bulk-jobs/{id}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // GET BULK JOB STATUS
    [HttpGet("bulk-jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(
        Guid id,
        CancellationToken ct)
    {
        if (!AdminId(out var admin))
            return Unauthorized();

        var job = await dal.GetJobAsync(admin, id, ct);

        return job == null ? NotFound() : Ok(job);
    }
}