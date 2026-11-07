using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AdminProfileDAL _adminDAL;
    private readonly IWebHostEnvironment _environment;
    private readonly AdminEventDAL _eventDAL;

    public AdminController(AdminProfileDAL adminDAL, AdminEventDAL eventDAL, IWebHostEnvironment environment)
    {
        _adminDAL = adminDAL;
        _eventDAL = eventDAL;
        _environment = environment;
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

        return claim != null && Guid.TryParse(claim.Value, out userId);
    }

    private async Task<bool> CanAdminAccessAsync(Guid userId)
    {
        return await _adminDAL.IsAdminEnabledAsync(userId);
    }

    #region "Dashboard"
    // GET: api/admin/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        if (!TryGetUserId(out Guid userId))
            return Unauthorized(new { message = "Invalid user token." });

        if (!await CanAdminAccessAsync(userId))
            return StatusCode(403, new
            {
                message = "Your admin account is disabled or inactive."
            });

        var profile = await _adminDAL.GetProfileAsync(userId);

        if (profile == null)
            return NotFound(new { message = "Admin account not found." });

        return Ok(new
        {
            message = "Admin dashboard loaded successfully.",
            profile.IsProfileCreated,
            profile
        });
    }
    #endregion

    #region "Profile"
    // GET: api/admin/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        if (!TryGetUserId(out Guid userId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(userId))
            return StatusCode(403, new { message = "Admin account disabled." });

        var profile = await _adminDAL.GetProfileAsync(userId);

        if (profile == null)
            return NotFound(new { message = "Admin not found." });

        if (!profile.IsProfileCreated)
        {
            return Ok(new
            {
                isProfileCreated = false,
                message = "Please create your admin profile.",
                profile
            });
        }

        return Ok(new
        {
            isProfileCreated = true,
            message = "Profile loaded successfully.",
            profile
        });
    }

    // POST: api/admin/profile
    // multipart/form-data
    [HttpPost("profile")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SaveProfile([FromForm] AdminProfileSaveDto dto)
    {
        if (!TryGetUserId(out Guid userId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(userId))
            return StatusCode(403, new { message = "Admin account disabled." });

        string? imageUrl = null;

        if (dto.ProfileImage != null)
        {
            var extension = Path.GetExtension(dto.ProfileImage.FileName)
                .ToLowerInvariant();

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only JPG, JPEG, PNG and WEBP images are allowed." });

            if (dto.ProfileImage.Length == 0 ||
                dto.ProfileImage.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "Image must be between 1 byte and 5 MB." });
            }

            var folder = Path.Combine(
                _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
                "uploads", "admins");

            Directory.CreateDirectory(folder);

            var fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(folder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await dto.ProfileImage.CopyToAsync(stream);

            imageUrl = $"/uploads/admins/{fileName}";
        }

        await _adminDAL.SaveProfileAsync(userId, dto, imageUrl);

        var updatedProfile = await _adminDAL.GetProfileAsync(userId);

        return Ok(new
        {
            message = "Admin profile saved successfully.",
            isProfileCreated = true,
            profile = updatedProfile
        });
    }

    #endregion

    #region "Event"

    // ----------------------------------------------------
    // EVENT IMAGE UPLOAD HELPER
    // ----------------------------------------------------

    private async Task<string?> SaveEventFileAsync(IFormFile? file, string folderName, bool required)
    {
        if (file == null)
        {
            if (required)
                throw new InvalidOperationException("Event Image 1 is required.");

            return null;
        }

        if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Each uploaded file must be between 1 byte and 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Only JPG, JPEG, PNG and WEBP files are allowed.");

        var root = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var folder = Path.Combine(root, "uploads", folderName);

        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(folder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/{folderName}/{fileName}";
    }

    // ----------------------------------------------------
    // CREATE EVENT
    // POST: api/admin/events
    // Content-Type: multipart/form-data
    // ----------------------------------------------------

    [HttpPost("events")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateEvent([FromForm] AdminEventSaveDto dto)
    {
        if (!TryGetUserId(out Guid adminUserId))
            return Unauthorized(new { message = "Invalid admin token." });

        if (!await CanAdminAccessAsync(adminUserId))
            return StatusCode(403, new { message = "Admin account disabled or inactive." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.Image1 == null)
            return BadRequest(new { message = "Event Image 1 is required." });

        try
        {
            var image1 = await SaveEventFileAsync(dto.Image1, "events", true);

            var image2 = await SaveEventFileAsync(dto.Image2, "events", false);

            var qrCode = await SaveEventFileAsync(dto.QRCode, "event-payment", false);

            int eventId = await _eventDAL.CreateAsync(adminUserId, dto, image1!, image2, qrCode);
            var createdEvent = await _eventDAL.GetByIdAsync(eventId, adminUserId);
            return CreatedAtAction(nameof(GetEventById), new { eventId }, new
                {
                    success = true,
                    message = "Event created successfully.",
                    eventId,
                    eventDetails = createdEvent
                });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ----------------------------------------------------
    // GET ALL EVENTS OF LOGGED-IN ADMIN
    // GET: api/admin/events
    // ----------------------------------------------------

    [HttpGet("events")]
    public async Task<IActionResult> GetMyEvents()
    {
        if (!TryGetUserId(out Guid adminUserId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(adminUserId))
            return StatusCode(403, new { message = "Admin account disabled." });

        var events = await _eventDAL.GetAllByAdminAsync(adminUserId);

        return Ok(new
        {
            success = true,
            totalRecords = events.Count,
            events
        });
    }

    // ----------------------------------------------------
    // GET EVENT BY ID
    // GET: api/admin/events/5
    // ----------------------------------------------------

    [HttpGet("events/{eventId:int}")]
    public async Task<IActionResult> GetEventById(int eventId)
    {
        if (!TryGetUserId(out Guid adminUserId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(adminUserId))
            return StatusCode(403, new { message = "Admin account disabled." });

        if (eventId <= 0)
            return BadRequest(new { message = "Invalid Event ID." });

        var eventDetails = await _eventDAL.GetByIdAsync(eventId, adminUserId);

        if (eventDetails == null)
            return NotFound(new
            {
                message = "Event not found or you do not own this event."
            });

        return Ok(new
        {
            success = true,
            eventDetails
        });
    }

    // ----------------------------------------------------
    // UPDATE EVENT
    // PUT: api/admin/events/5
    // Content-Type: multipart/form-data
    // ----------------------------------------------------

    [HttpPut("events/{eventId:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateEvent(int eventId, [FromForm] AdminEventSaveDto dto)
    {
        if (!TryGetUserId(out Guid adminUserId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(adminUserId))
            return StatusCode(403, new { message = "Admin account disabled." });

        if (eventId <= 0)
            return BadRequest(new { message = "Invalid Event ID." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _eventDAL.GetByIdAsync(eventId, adminUserId);

        if (existing == null)
            return NotFound(new
            {
                message = "Event not found or you do not own this event."
            });

        try
        {
            // Keep old image if no replacement is uploaded.
            var image1 = existing.Image1;
            var image2 = existing.Image2;
            var qrCode = existing.QRCodeUrl;

            if (dto.Image1 != null)
                image1 = (await SaveEventFileAsync(dto.Image1, "events", true))!;

            if (dto.Image2 != null)
                image2 = await SaveEventFileAsync(dto.Image2, "events", false);

            if (dto.QRCode != null)
                qrCode = await SaveEventFileAsync(dto.QRCode, "event-payment", false);

            bool updated = await _eventDAL.UpdateAsync(eventId, adminUserId, dto, image1, image2, qrCode);

            if (!updated)
                return NotFound(new { message = "Event not found." });

            var updatedEvent = await _eventDAL.GetByIdAsync(eventId, adminUserId);

            return Ok(new
            {
                success = true,
                message = "Event updated successfully.",
                eventDetails = updatedEvent
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ----------------------------------------------------
    // ADMIN ENABLE / DISABLE EVENT
    // PATCH: api/admin/events/5/status
    // ----------------------------------------------------

    [HttpPatch("events/{eventId:int}/status")]
    public async Task<IActionResult> UpdateEventStatus(int eventId, [FromBody] AdminEventStatusDto dto)
    {
        if (!TryGetUserId(out Guid adminUserId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(adminUserId))
            return StatusCode(403, new { message = "Admin account disabled." });

        if (eventId <= 0)
            return BadRequest(new { message = "Invalid Event ID." });

        if (dto == null)
            return BadRequest(new { message = "Invalid request." });

        bool updated = await _eventDAL.UpdateAdminStatusAsync(eventId, adminUserId, dto.nBit);

        if (!updated)
            return NotFound(new
            {
                message = "Event not found or you do not own this event."
            });

        return Ok(new
        {
            success = true,
            eventId,
            nBit = dto.nBit,
            message = dto.nBit ? "Event enabled by Admin." : "Event disabled by Admin."
        });
    }

    #endregion


    [HttpGet("events/{eventId:int}/registrations")]
    public async Task<IActionResult> GetEventRegistrations(int eventId, [FromServices] EventRegistrationDAL registrationDAL)
    {
        if (!TryGetUserId(out Guid adminUserId))
            return Unauthorized();

        if (!await CanAdminAccessAsync(adminUserId))
            return StatusCode(403, new
            {
                message = "Admin account disabled."
            });

        var eventDetails =
            await _eventDAL.GetByIdAsync(eventId, adminUserId);

        if (eventDetails == null)
            return NotFound(new
            {
                message = "Event not found or you do not own this event."
            });

        var registrations =
            await registrationDAL.GetEventRegistrationsAsync(eventId);

        return Ok(new
        {
            success = true,
            eventId,
            eventName = eventDetails.EventTitle,
            totalRecords = registrations.Count,
            registrations
        });
    }

}
