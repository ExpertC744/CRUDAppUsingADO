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

    public AdminController(
        AdminProfileDAL adminDAL,
        IWebHostEnvironment environment)
    {
        _adminDAL = adminDAL;
        _environment = environment;
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("sub");

        return claim != null && Guid.TryParse(claim.Value, out userId);
    }

    private async Task<bool> CanAdminAccessAsync(Guid userId)
    {
        return await _adminDAL.IsAdminEnabledAsync(userId);
    }

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
    public async Task<IActionResult> SaveProfile(
        [FromForm] AdminProfileSaveDto dto)
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
}