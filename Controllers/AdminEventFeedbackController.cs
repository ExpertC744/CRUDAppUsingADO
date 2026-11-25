using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("admin/events")]
    [Authorize(Roles = "Admin")]
    public class AdminEventFeedbackController : ControllerBase
    {
        private readonly EventFeedbackDAL _feedbackDAL;
        private readonly AdminEventDAL _eventDAL;
        private readonly AdminProfileDAL _adminProfileDAL;

        public AdminEventFeedbackController(
            EventFeedbackDAL feedbackDAL,
            AdminEventDAL eventDAL,
            AdminProfileDAL adminProfileDAL)
        {
            _feedbackDAL = feedbackDAL;
            _eventDAL = eventDAL;
            _adminProfileDAL = adminProfileDAL;
        }

        private bool TryGetAdminId(out Guid adminId)
        {
            adminId = Guid.Empty;

            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub");

            return Guid.TryParse(userId, out adminId);
        }

        private async Task<bool> IsEnabledAdminAsync(Guid adminId)
        {
            return await _adminProfileDAL.IsAdminEnabledAsync(adminId);
        }

        // =========================================================
        // GET: /admin/events/{eventId}/feedback
        // =========================================================
        [HttpGet("{eventId:int}/feedback")]
        public async Task<IActionResult> GetEventFeedback(
            int eventId,
            CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid admin token."
                });

            if (!await IsEnabledAdminAsync(adminId))
                return Forbid();

            if (eventId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Event ID."
                });

            // Verify that the event belongs to this Admin.
            var eventDetails =
                await _eventDAL.GetByIdAsync(eventId, adminId);

            if (eventDetails == null)
                return NotFound(new
                {
                    success = false,
                    message = "Event not found or you do not own this event."
                });

            var feedback =
                await _feedbackDAL.GetAdminEventFeedbackAsync(
                    eventId, adminId, ct);

            return Ok(new
            {
                success = true,
                eventId,
                eventTitle = eventDetails.EventTitle,
                total = feedback.Count,
                feedback
            });
        }

        // =========================================================
        // PATCH: /admin/events/{eventId}/feedback/{feedbackId}/approval
        // =========================================================
        [HttpPatch("{eventId:int}/feedback/{feedbackId:int}/approval")]
        public async Task<IActionResult> UpdateFeedbackApproval(
            int eventId,
            int feedbackId,
            [FromBody] EventFeedbackApprovalDto dto,
            CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid admin token."
                });

            if (!await IsEnabledAdminAsync(adminId))
                return Forbid();

            if (eventId <= 0 || feedbackId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Event ID or Feedback ID."
                });

            // Check event ownership.
            var eventDetails =
                await _eventDAL.GetByIdAsync(eventId, adminId);

            if (eventDetails == null)
                return NotFound(new
                {
                    success = false,
                    message = "Event not found or you do not own this event."
                });

            bool updated =
                await _feedbackDAL.UpdateApprovalAsync(
                    eventId,
                    feedbackId,
                    adminId,
                    dto.IsApproved,
                    ct);

            if (!updated)
                return NotFound(new
                {
                    success = false,
                    message = "Feedback not found for this event."
                });

            return Ok(new
            {
                success = true,
                eventId,
                feedbackId,
                isApproved = dto.IsApproved,
                message = dto.IsApproved
                    ? "Feedback approved and enabled successfully."
                    : "Feedback disabled successfully."
            });
        }
    }
}