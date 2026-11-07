//using CRUDAppUsingADO.DataAccess;
//using CRUDAppUsingADO.Models;
//using Microsoft.AspNetCore.Mvc;
//using System.Diagnostics;

//namespace CRUDAppUsingADO.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class HomeController : ControllerBase
//    {
//        private readonly FeedbackDAL _feedbackDAL;

//        // Replace this with the actual admin UserId from your database.
//        private const string AdminUserId = "ADMIN_USER_ID";

//        public HomeController(FeedbackDAL feedbackDAL)
//        {
//            _feedbackDAL = feedbackDAL;
//        }

//        // GET: /api/home/feedback
//        // Public endpoint; no authentication token required.
//        [HttpGet("feedback")]
//        public async Task<IActionResult> GetHomeFeedback()
//        {
//            var adminFeedback =
//                await _feedbackDAL.GetLatestAdminFeedbackAsync(AdminUserId);

//            var otherFeedback =
//                await _feedbackDAL.GetOtherFeedbackAsync(AdminUserId);

//            return Ok(new
//            {
//                adminFeedback,
//                otherFeedback
//            });
//        }

//    }
//}


using CRUDAppUsingADO.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly FeedbackDAL _feedbackDAL;
        private readonly AdminEventDAL _eventDAL;

        private const string AdminUserId = "ADMIN_USER_ID";

        public HomeController(
            FeedbackDAL feedbackDAL,
            AdminEventDAL eventDAL)
        {
            _feedbackDAL = feedbackDAL;
            _eventDAL = eventDAL;
        }

        // GET: api/home/feedback
        [HttpGet("feedback")]
        public async Task<IActionResult> GetHomeFeedback()
        {
            var adminFeedback =
                await _feedbackDAL.GetLatestAdminFeedbackAsync(AdminUserId);

            var otherFeedback =
                await _feedbackDAL.GetOtherFeedbackAsync(AdminUserId);

            return Ok(new
            {
                adminFeedback,
                otherFeedback
            });
        }

        // GET: api/home/events
        [HttpGet("events")]
        public async Task<IActionResult> GetEvents()
        {
            var events = await _eventDAL.GetPublicEventsAsync();

            return Ok(new
            {
                success = true,
                totalRecords = events.Count,
                events
            });
        }

        // GET: api/home/events/2
        [HttpGet("events/{eventId:int}")]
        public async Task<IActionResult> GetEventDetails(int eventId)
        {
            if (eventId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Event ID."
                });

            var eventDetails =
                await _eventDAL.GetPublicEventByIdAsync(eventId);

            if (eventDetails == null)
                return NotFound(new
                {
                    success = false,
                    message = "Event not found, expired or unavailable."
                });

            return Ok(new
            {
                success = true,
                eventDetails
            });
        }
    }
}
