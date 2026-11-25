using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("home/event-feedback")]
    public class HomeEventFeedbackController : ControllerBase
    {
        private readonly EventFeedbackDAL _feedbackDAL;
        private readonly AdminEventDAL _eventDAL;

        public HomeEventFeedbackController(
            EventFeedbackDAL feedbackDAL,
            AdminEventDAL eventDAL)
        {
            _feedbackDAL = feedbackDAL;
            _eventDAL = eventDAL;
        }

        // =========================================================
        // POST: /home/event-feedback
        // Submit feedback for a registered event participant
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> SubmitFeedback(
            [FromBody] EventFeedbackSubmitDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Please provide valid feedback details.",
                    errors = ModelState
                });

            if (dto.EventId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Event ID."
                });

            // Verify that this event exists and is publicly available.
            var eventDetails =
                await _eventDAL.GetPublicEventByIdAsync(dto.EventId);

            if (eventDetails == null)
                return BadRequest(new
                {
                    success = false,
                    message = "Event not found or unavailable."
                });

            try
            {
                int feedbackId =
                    await _feedbackDAL.SubmitFeedbackAsync(dto, ct);

                return Ok(new
                {
                    success = true,
                    feedbackId,
                    eventId = dto.EventId,
                    message = "Feedback submitted successfully. It will be displayed after approval by the event Admin."
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(new
                {
                    success = false,
                    message = "You have already submitted feedback for this event."
                });
            }
        }

        // =========================================================
        // GET: /home/event-feedback/{eventId}
        // Public API - only approved feedback
        // =========================================================
        [HttpGet("{eventId:int}")]
        public async Task<IActionResult> GetApprovedFeedback(
            int eventId,
            CancellationToken ct)
        {
            if (eventId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Event ID."
                });

            var feedback =
                await _feedbackDAL.GetApprovedFeedbackAsync(eventId, ct);

            return Ok(new
            {
                success = true,
                eventId,
                total = feedback.Count,
                feedback
            });
        }
    }
}