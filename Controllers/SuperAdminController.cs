
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/superadmin")]
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : ControllerBase
    {
        private readonly FeedbackDAL _feedbackDAL;
        private readonly AdminProfileDAL _adminProfileDAL;

        public SuperAdminController(
            FeedbackDAL feedbackDAL,
            AdminProfileDAL adminProfileDAL)
        {
            _feedbackDAL = feedbackDAL;
            _adminProfileDAL = adminProfileDAL;
        }

        // GET: api/superadmin/dashboard
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return Ok(new
            {
                success = true,
                message = "SuperAdmin token validated successfully.",
                admin = User.Identity?.Name
            });
        }

        // GET: api/superadmin/feedback
        [HttpGet("feedback")]
        public async Task<IActionResult> GetAllFeedback()
        {
            var feedbackList =
                await _feedbackDAL.GetAllFeedbackForSuperAdminAsync();

            return Ok(new
            {
                success = true,
                count = feedbackList.Count,
                feedback = feedbackList
            });
        }

        // PATCH: api/superadmin/feedback/5/status
        [HttpPatch("feedback/{feedbackId:int}/status")]
        public async Task<IActionResult> SetFeedbackStatus(
            int feedbackId,
            [FromBody] FeedbackStatusDto dto)
        {
            if (feedbackId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid feedback ID."
                });
            }

            if (dto == null)
                return BadRequest(new { message = "Invalid request." });

            bool updated = await _feedbackDAL.SetFeedbackStatusAsync(
                feedbackId,
                dto.nBit
            );

            if (!updated)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Feedback record not found."
                });
            }

            return Ok(new
            {
                success = true,
                feedbackId,
                nBit = dto.nBit,
                message = dto.nBit
                    ? "Feedback enabled successfully."
                    : "Feedback disabled successfully."
            });
        }

        // GET: api/superadmin/admins
        // Returns all Admin users.
        [HttpGet("admins")]
        public async Task<IActionResult> GetAdmins()
        {
            var admins = await _adminProfileDAL.GetAdminsAsync();

            return Ok(new
            {
                success = true,
                message = "Admin list loaded successfully.",
                totalRecords = admins.Count,
                admins
            });
        }

        // PATCH: api/superadmin/admins/{userId}/status
        // Body: { "nBit": true } or { "nBit": false }
        [HttpPatch("admins/{userId:guid}/status")]
        public async Task<IActionResult> UpdateAdminStatus(
            Guid userId,
            [FromBody] AdminStatusDto dto)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid admin UserId."
                });
            }

            if (dto == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid request."
                });
            }

            bool updated = await _adminProfileDAL.UpdateAdminStatusAsync(
                userId,
                dto.nBit
            );

            if (!updated)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Admin not found."
                });
            }

            return Ok(new
            {
                success = true,
                userId,
                nBit = dto.nBit,
                message = dto.nBit
                    ? "Admin enabled successfully."
                    : "Admin disabled successfully."
            });
        }
    }
}
