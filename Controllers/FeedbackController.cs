
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/feedback")]
    [Authorize]
    public class FeedbackController : ControllerBase
    {
        private readonly FeedbackDAL _feedbackDAL;

        public FeedbackController(FeedbackDAL feedbackDAL)
        {
            _feedbackDAL = feedbackDAL;
        }

        // GET: api/feedback/me
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            string? userId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            string? userName =
                User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("unique_name")?.Value
                ?? User.FindFirst("name")?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(userName))
            {
                return Unauthorized(new
                {
                    message = "User ID or username is missing from the token."
                });
            }

            return Ok(new FeedbackUserDto
            {
                UserId = userId,
                UserName = userName
            });
        }

        // POST: api/feedback
        [HttpPost]
        public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackDto dto)
        {
            string? userId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            string? userName =
                User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("unique_name")?.Value
                ?? User.FindFirst("name")?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName))
            {
                return Unauthorized(new
                {
                    message = "User ID or username is missing from the token."
                });
            }

            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new
                {
                    message = "Please select a rating between 1 and 5."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length > 240)
            {
                return BadRequest(new
                {
                    message = "Description is required and must not exceed 240 characters."
                });
            }

            int feedbackId = await _feedbackDAL.AddFeedbackAsync(
                userId,
                userName,
                dto);

            return Ok(new
            {
                message = "Feedback submitted successfully.",
                feedbackId,
                userId,
                userName,
                dto.Rating,
                description = dto.Description.Trim()
            });
        }
    }
}
