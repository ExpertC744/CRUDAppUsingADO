using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly FeedbackDAL _feedbackDAL;
        private readonly AdminEventDAL _eventDAL;
        private readonly SubscriptionDAL _subscriptionDal;
        private readonly PromoCodeDAL _promoCodeDAL;
        private readonly ContactUsDAL _contactUsDAL;

        private const string AdminUserId = "ADMIN_USER_ID";

        public HomeController(
    FeedbackDAL feedbackDAL,
    AdminEventDAL eventDAL,
    SubscriptionDAL subscriptionDal,
    PromoCodeDAL promoCodeDAL,
    ContactUsDAL contactUsDAL)
        {
            _feedbackDAL = feedbackDAL;
            _eventDAL = eventDAL;
            _subscriptionDal = subscriptionDal;
            _promoCodeDAL = promoCodeDAL;
            _contactUsDAL = contactUsDAL;
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

        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _subscriptionDal.GetPlansAsync(true);

            return Ok(new ApiResponse<List<SubscriptionPlanDto>>
            {
                Success = true,
                Message = "Available plans retrieved successfully.",
                Data = plans
            });
        }

        [HttpGet("plans/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlan(int id)
        {
            var plan = await _subscriptionDal.GetPlanAsync(id);

            if (plan == null || !plan.IsActive)
                return NotFound(new { success = false, message = "Plan not found." });

            return Ok(new ApiResponse<SubscriptionPlanDto>
            {
                Success = true,
                Message = "Plan retrieved successfully.",
                Data = plan
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

        // GET: api/home/promocodes
        [HttpGet("promocodes")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicPromoCodes()
        {
            var promos = await _promoCodeDAL.GetPublicActiveAsync();

            return Ok(new
            {
                success = true,
                totalRecords = promos.Count,
                data = promos
            });
        }


        // POST: api/home/contact
        [HttpPost("contact")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitContactMessage(
            [FromBody] ContactUsCreateDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                long contactId = await _contactUsDAL.InsertAsync(dto, ct);

                return Ok(new
                {
                    success = true,
                    contactId,
                    message = "Thank you for contacting us. Your message has been submitted successfully."
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to submit your message right now. Please try again later."
                });
            }
        }

    }
}
