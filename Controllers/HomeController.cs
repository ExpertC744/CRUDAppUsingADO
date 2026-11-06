using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly FeedbackDAL _feedbackDAL;

        // Replace this with the actual admin UserId from your database.
        private const string AdminUserId = "ADMIN_USER_ID";

        public HomeController(FeedbackDAL feedbackDAL)
        {
            _feedbackDAL = feedbackDAL;
        }

        // GET: /api/home/feedback
        // Public endpoint; no authentication token required.
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

    }
}
