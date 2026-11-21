using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("admin/event-gallery")]
    [Authorize(Roles = "Admin")]
    public class EventGalleryController : ControllerBase
    {
        private readonly EventGalleryDAL _galleryDAL;
        private readonly IWebHostEnvironment _environment;

        private const int MaxImagesPerEvent = 12;
        private const int MaxImageSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        public EventGalleryController(EventGalleryDAL galleryDAL, IWebHostEnvironment environment)
        {
            _galleryDAL = galleryDAL;
            _environment = environment;
        }

        private bool TryGetAdminUserId(out Guid adminUserId)
        {
            adminUserId = Guid.Empty;
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(userId, out adminUserId);
        }

        // ==========================================
        // POST: api/admin/event-gallery/upload
        // Upload maximum 12 images per event
        // ==========================================
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(65 * 1024 * 1024)]
        public async Task<IActionResult> UploadImages([FromForm] int eventId, [FromForm] List<IFormFile> images)
        {
            if (!TryGetAdminUserId(out Guid adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid admin token."
                });
            }

            if (eventId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please select a valid event."
                });
            }

            if (images == null || images.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please select at least one image."
                });
            }

            if (images.Count > MaxImagesPerEvent)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "You can select maximum 12 images in one upload."
                });
            }

            // Validate all images before saving any file
            foreach (var image in images)
            {
                if (image == null || image.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "An empty image was selected."
                    });
                }

                if (image.Length > MaxImageSize)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Image '{image.FileName}' exceeds the 5 MB limit."
                    });
                }

                string extension = Path.GetExtension(image.FileName).ToLowerInvariant();

                if (!AllowedExtensions.Contains(extension))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Invalid image format: {image.FileName}. Only JPG, JPEG, PNG and WEBP are allowed."
                    });
                }

                if (!AllowedContentTypes.Contains(image.ContentType.ToLowerInvariant()))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Invalid image content type: {image.FileName}."
                    });
                }
            }

            string root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            string relativeFolder = Path.Combine("uploads", "event-gallery", eventId.ToString());

            string folder = Path.Combine(root, relativeFolder);

            Directory.CreateDirectory(folder);

            var savedFilePaths = new List<string>();
            var imageRecords = new List<EventGalleryImageDto>();

            try
            {
                foreach (var image in images)
                {
                    string extension = Path.GetExtension(image.FileName).ToLowerInvariant();

                    // Never use the original filename as the physical filename
                    string newFileName = $"{Guid.NewGuid():N}{extension}";

                    string physicalPath = Path.Combine(folder, newFileName);

                    await using (var stream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        await image.CopyToAsync(stream);
                    }

                    savedFilePaths.Add(physicalPath);

                    string imagePath = $"/uploads/event-gallery/{eventId}/{newFileName}";

                    imageRecords.Add(new EventGalleryImageDto
                    {
                        EventId = eventId,
                        ImageName = Path.GetFileName(image.FileName),
                        ImagePath = imagePath
                    });
                }

                // Save all image records in one database transaction
                await _galleryDAL.AddImagesAsync(
                    eventId,
                    adminUserId,
                    imageRecords);

                return Ok(new
                {
                    success = true,
                    message = "Event gallery images uploaded successfully.",
                    eventId,
                    uploadedCount = imageRecords.Count,
                    images = imageRecords.Select(x => new
                    {
                        x.ImageName,
                        x.ImagePath,
                        imageUrl = $"{Request.Scheme}://{Request.Host}{x.ImagePath}"
                    })
                });
            }
            catch (InvalidOperationException ex)
            {
                // Delete files if event ownership or 12-image limit fails
                foreach (var path in savedFilePaths)
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch
            {
                // Cleanup files if database operation fails
                foreach (var path in savedFilePaths)
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                throw;
            }
        }

        // ==========================================
        // GET: api/admin/event-gallery/5
        // Get all images for selected event
        // ==========================================
        [HttpGet("{eventId:int}")]
        public async Task<IActionResult> GetEventImages(int eventId)
        {
            if (!TryGetAdminUserId(out Guid adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid admin token."
                });
            }

            if (eventId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid event ID."
                });
            }

            var images = await _galleryDAL.GetImagesAsync(
                eventId,
                adminUserId);

            return Ok(new
            {
                success = true,
                eventId,
                totalImages = images.Count,
                remainingImages = MaxImagesPerEvent - images.Count,
                data = images.Select(x => new
                {
                    x.GalleryImageId,
                    x.EventId,
                    x.ImageName,
                    x.ImagePath,
                    imageUrl = $"{Request.Scheme}://{Request.Host}{x.ImagePath}",
                    x.CreatedAt
                })
            });
        }
    }
}
