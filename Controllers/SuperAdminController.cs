
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

        public SuperAdminController(FeedbackDAL feedbackDAL)
        {
            _feedbackDAL = feedbackDAL;
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
        // Returns all feedback, newest first.
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
        // Body: { "nBit": true } or { "nBit": false }
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
    }
}
