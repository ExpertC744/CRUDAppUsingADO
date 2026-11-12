using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models.Bulk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class BulkCardController(BulkCardDAL dal, IWebHostEnvironment env) : ControllerBase
{
    private bool AdminId(out Guid id)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out id);
    }

    private static string NormalizeFormat(string? value)
    {
        return (value ?? "").Trim().ToUpperInvariant() switch
        {
            "PNG" => "PNG",
            "JPG" or "JPEG" => "JPG",
            "PDF" => "PDF",
            _ => ""
        };
    }

    private static string NormalizeTemplateType(string? value)
    {
        return (value ?? "").Trim().ToLowerInvariant() switch
        {
            "invitation" => "Invitation",
            "greeting" => "Greeting",
            "certificate" => "Certificate",
            _ => ""
        };
    }

    private static async Task<bool> HasValidFileSignatureAsync(IFormFile file, string format, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();

        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(), ct);

        return format switch
        {
            "PNG" => read >= 8 &&
                     header.SequenceEqual(new byte[]
                     {
                         0x89, 0x50, 0x4E, 0x47,
                         0x0D, 0x0A, 0x1A, 0x0A
                     }),

            "JPG" => read >= 3 &&
                     header[0] == 0xFF &&
                     header[1] == 0xD8 &&
                     header[2] == 0xFF,

            "PDF" => read >= 5 &&
                     header[0] == 0x25 &&
                     header[1] == 0x50 &&
                     header[2] == 0x44 &&
                     header[3] == 0x46 &&
                     header[4] == 0x2D,

            _ => false
        };
    }

    // ==========================================
    // SAVE CARD DESIGN
    // POST: api/admin/cards
    // ==========================================
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

        // Normalize incoming values.
        var templateType = NormalizeTemplateType(dto.TemplateType);
        var uploadFormat = NormalizeFormat(dto.UploadFormat);
        var outputFormat = NormalizeFormat(dto.OutputFormat);

        // Validate normalized values.
        if (string.IsNullOrEmpty(templateType))
            return BadRequest("Invalid TemplateType. Use Invitation, Greeting or Certificate.");

        if (string.IsNullOrEmpty(uploadFormat))
            return BadRequest("Invalid UploadFormat. Use PNG, JPG or PDF.");

        if (string.IsNullOrEmpty(outputFormat))
            return BadRequest("Invalid OutputFormat. Use PNG, JPG or PDF.");

        if (dto.Image == null ||
            dto.Image.Length == 0 ||
            dto.Image.Length > 10_000_000)
        {
            return BadRequest("A source file is required and must be under 10 MB.");
        }

        var ext = Path.GetExtension(dto.Image.FileName)
            .ToLowerInvariant();

        var actualFormat = ext switch
        {
            ".png" => "PNG",
            ".jpg" or ".jpeg" => "JPG",
            ".pdf" => "PDF",
            _ => ""
        };

        if (string.IsNullOrEmpty(actualFormat))
            return BadRequest("Only PNG, JPG and PDF files are accepted.");

        if (actualFormat != uploadFormat)
            return BadRequest("The selected UploadFormat does not match the uploaded file.");

        if (!await HasValidFileSignatureAsync(
                dto.Image, actualFormat, ct))
        {
            return BadRequest("The uploaded file content does not match its file format.");
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

        var dir = Path.Combine(webRoot, "uploads", "card-designs");

        Directory.CreateDirectory(dir);

        // Store using canonical extension.
        var storedExtension = actualFormat switch
        {
            "PNG" => ".png",
            "JPG" => ".jpg",
            "PDF" => ".pdf",
            _ => throw new InvalidOperationException("Invalid file format.")
        };

        //var file = Guid.NewGuid().ToString("N") + storedExtension;
        //var path = Path.Combine(dir, file);

        //await using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        //{
        //    await dto.Image.CopyToAsync(fs, ct);
        //}

        //var imageUrl = $"/uploads/card-designs/{file}";
        // ===== UPDATED (START) =====
        // file name = user name from the table (frontend sends it as DesignName)
        //---------------------part2
        //var userName = string.Concat((dto.DesignName ?? "").Split(Path.GetInvalidFileNameChars())).Trim();
        //if (string.IsNullOrWhiteSpace(userName)) userName = "user";

        //var file = userName + storedExtension;   // Rahul Patil.png
        //var path = Path.Combine(dir, file);

        //// FileMode.Create -> same name again replaces the old file
        //await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        //{
        //    await dto.Image.CopyToAsync(fs, ct);
        //}

        //// spaces become %20 so the URL works in a link
        //var imageUrl = $"/uploads/card-designs/{Uri.EscapeDataString(file)}";

        //------------part 3 
        var file = Guid.NewGuid().ToString("N")
           + storedExtension;

        var path = Path.Combine(dir, file);

        await using (var fs = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await dto.Image.CopyToAsync(fs, ct);
        }

        var imageUrl = $"/uploads/card-designs/{file}";

        // ===== UPDATED (END) =====

        try
        {
            var id = await dal.SaveDesignAsync(
    admin,
    dto.DesignName.Trim(),
    imageUrl,
    dto.CanvasWidth,
    dto.CanvasHeight,
    dto.CanvasJson,
    templateType,
    uploadFormat,
    outputFormat,
    ct);

            return Ok(new
            {
                success = true,
                cardDesignId = id,
                designName = dto.DesignName,
                templateType = templateType,
                uploadFormat = uploadFormat,
                outputFormat = outputFormat,
                imageUrl
            });
        }
        catch
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);

            throw;
        }
    }

    // ==========================================
    // CREATE BULK JOB
    // POST: api/admin/bulk-jobs
    // ==========================================
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

    // ==========================================
    // GET BULK JOB STATUS
    // GET: api/admin/bulk-jobs/{id}
    // ==========================================
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



    [HttpGet("bulk-jobs")]
    public async Task<IActionResult> GetAllBulkJobs(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!AdminId(out var admin))
            return Unauthorized();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var allowedStatuses = new[]
            {
            "Queued",
            "Scheduled",
            "Processing",
            "Completed",
            "Failed"
        };

            if (!allowedStatuses.Contains(
                status,
                StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "Invalid status. Use Queued, Scheduled, Processing, Completed or Failed.");
            }

            status = allowedStatuses.First(
                x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            status = null;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await dal.GetJobsAsync(
            admin, status, page, pageSize, ct);

        return Ok(new
        {
            success = true,
            totalRecords = result.totalRecords,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(
                result.totalRecords / (double)pageSize),
            data = result.data
        });
    }



    [HttpGet("bulk-jobs/{id:guid}/recipients")]
    public async Task<IActionResult> GetBulkJobRecipients(
        Guid id,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!AdminId(out var admin))
            return Unauthorized();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var allowedStatuses = new[]
            {
            "Pending",
            "Processing",
            "Success",
            "Failed"
        };

            if (!allowedStatuses.Contains(
                status,
                StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "Invalid recipient status. Use Pending, Processing, Success or Failed.");
            }

            status = allowedStatuses.First(
                x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            status = null;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await dal.GetJobRecipientsAsync(
            admin, id, status, page, pageSize, ct);

        if (result == null)
            return NotFound(new
            {
                success = false,
                message = "Bulk job not found."
            });

        return Ok(new
        {
            success = true,
            jobId = id,
            totalRecords = result.TotalRecords,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
            data = result.Data
        });
    }

}