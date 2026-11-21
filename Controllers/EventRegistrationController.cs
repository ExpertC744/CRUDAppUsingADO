
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Mvc;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("event-registration")]
    public class EventRegistrationController : ControllerBase
    {
        private readonly AdminEventDAL _eventDAL;
        private readonly EventRegistrationDAL _registrationDAL;
        private readonly IWebHostEnvironment _environment;

        public EventRegistrationController(
            AdminEventDAL eventDAL,
            EventRegistrationDAL registrationDAL,
            IWebHostEnvironment environment)
        {
            _eventDAL = eventDAL;
            _registrationDAL = registrationDAL;
            _environment = environment;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(35 * 1024 * 1024)]
        public async Task<IActionResult> Register(
            [FromForm] EventRegistrationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.EventId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Event ID."
                });

            if (dto.RegistrationType != "Individual" &&
                dto.RegistrationType != "Group")
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid registration type."
                });

            if (dto.Participants == null ||
                dto.Participants.Count == 0 ||
                dto.Participants.Count > 30)
                return BadRequest(new
                {
                    success = false,
                    message = "Registration must contain between 1 and 30 participants."
                });

            if (dto.RegistrationType == "Individual" &&
                dto.Participants.Count != 1)
                return BadRequest(new
                {
                    success = false,
                    message = "Individual registration must have exactly one participant."
                });

            if (dto.RegistrationType == "Group")
            {
                if (string.IsNullOrWhiteSpace(dto.GroupName))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Group name is required."
                    });

                if (string.IsNullOrWhiteSpace(dto.TeamLeadName))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Team lead name is required."
                    });

                if (dto.Participants.Count < 2)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Group registration must contain at least 2 participants."
                    });
            }

            var eventDetails =
                await _eventDAL.GetPublicEventByIdAsync(dto.EventId);

            if (eventDetails == null)
                return BadRequest(new
                {
                    success = false,
                    message = "Event is unavailable or registration has expired."
                });

            // Validate each participant
            for (int i = 0; i < dto.Participants.Count; i++)
            {
                var p = dto.Participants[i];

                if (string.IsNullOrWhiteSpace(p.FullName))
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Participant {i + 1}: Full name is required."
                    });

                if (p.DOB == default ||
                    p.DOB.Date > DateTime.Today ||
                    CalculateAge(p.DOB) > 120)
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Participant {i + 1}: Please enter a valid date of birth."
                    });

                if (p.Gender != "Male" && p.Gender != "Female")
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Participant {i + 1}: Invalid gender."
                    });

                if (!new[] { "Student", "Fresher", "Experience" }
                    .Contains(p.RegistrationCategory))
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Participant {i + 1}: Invalid registration category."
                    });

                // Student and Fresher validation
                if (p.RegistrationCategory == "Student" ||
                    p.RegistrationCategory == "Fresher")
                {
                    if (string.IsNullOrWhiteSpace(p.CollegeName))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: College name is required."
                        });

                    if (string.IsNullOrWhiteSpace(p.Stream))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: Stream is required."
                        });

                    if (string.IsNullOrWhiteSpace(p.Branch))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: Branch is required."
                        });

                    if (string.IsNullOrWhiteSpace(p.AcademicYear))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: Academic year is required."
                        });

                    // Do not store professional fields for Student/Fresher
                    p.OrganizationName = null;
                    p.Designation = null;
                    p.Experience = null;
                }

                // Working Professional validation
                if (p.RegistrationCategory == "Experience")
                {
                    if (string.IsNullOrWhiteSpace(p.OrganizationName))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: Organization name is required."
                        });

                    if (string.IsNullOrWhiteSpace(p.Designation))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: Designation is required."
                        });

                    if (string.IsNullOrWhiteSpace(p.Experience))
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Participant {i + 1}: Experience is required."
                        });

                    // Do not store college fields for Experience
                    p.CollegeName = null;
                    p.Stream = null;
                    p.Branch = null;
                    p.AcademicYear = null;
                }
            }

            var savedFiles = new List<string>();

            try
            {
                var imagePaths = new List<string?>();

                var root = _environment.WebRootPath ??
                    Path.Combine(_environment.ContentRootPath, "wwwroot");

                var folder = Path.Combine(
                    root, "uploads", "event-participants");

                Directory.CreateDirectory(folder);

                foreach (var participant in dto.Participants)
                {
                    string? imageUrl = null;

                    if (participant.ProfileImage != null)
                    {
                        var file = participant.ProfileImage;

                        if (file.Length == 0 ||
                            file.Length > 5 * 1024 * 1024)
                            return BadRequest(new
                            {
                                success = false,
                                message = "Each profile image must be between 1 byte and 5 MB."
                            });

                        var extension = Path.GetExtension(file.FileName)
                            .ToLowerInvariant();

                        var allowed = new[]
                        {
                            ".jpg", ".jpeg", ".png", ".svg"
                        };

                        if (!allowed.Contains(extension))
                            return BadRequest(new
                            {
                                success = false,
                                message = "Only JPG, JPEG, PNG and SVG are allowed."
                            });

                        var fileName = $"{Guid.NewGuid():N}{extension}";
                        var filePath = Path.Combine(folder, fileName);

                        await using (var stream = new FileStream(
                            filePath, FileMode.CreateNew))
                        {
                            await file.CopyToAsync(stream);
                        }

                        imageUrl = $"/uploads/event-participants/{fileName}";
                        savedFiles.Add(filePath);
                    }

                    imagePaths.Add(imageUrl);
                }

                int registrationId =
                    await _registrationDAL.SaveRegistrationAsync(
                        dto,
                        imagePaths,
                        eventDetails.EntryFee);

                return Ok(new
                {
                    success = true,
                    message = "Event registration completed successfully.",
                    registrationId,
                    eventId = dto.EventId,
                    registrationType = dto.RegistrationType,
                    totalParticipants = dto.Participants.Count,
                    totalAmount = eventDetails.EntryFee *
                                  dto.Participants.Count
                });
            }
            catch
            {
                foreach (var path in savedFiles)
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                throw;
            }
        }

        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            int age = today.Year - dob.Year;

            if (dob.Date > today.AddYears(-age))
                age--;

            return age;
        }
    }
}
