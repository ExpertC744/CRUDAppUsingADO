using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CRUDAppUsingADO.Models.Attendance;
using CRUDAppUsingADO.Services;
using System.Security.Cryptography;
using System.Text;

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
        private readonly EventRegistrationDAL _registrationDAL;
        private readonly EventAttendanceDAL _attendanceDAL;
        private readonly SendGridEmailService _emailService;

        private const string AdminUserId = "ADMIN_USER_ID";

        public HomeController(FeedbackDAL feedbackDAL, AdminEventDAL eventDAL, SubscriptionDAL subscriptionDal, PromoCodeDAL promoCodeDAL, ContactUsDAL contactUsDAL, EventRegistrationDAL registrationDAL,
        EventAttendanceDAL attendanceDAL, SendGridEmailService emailService)
        {
            _feedbackDAL = feedbackDAL;
            _eventDAL = eventDAL;
            _subscriptionDal = subscriptionDal;
            _promoCodeDAL = promoCodeDAL;
            _contactUsDAL = contactUsDAL;
            _registrationDAL = registrationDAL;
            _attendanceDAL = attendanceDAL;
            _emailService = emailService;
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

        private static string HashOtp(string otp)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
            return Convert.ToHexString(bytes);
        }

        private static DateTime GetIndiaDate()
        {
            TimeZoneInfo indiaTimeZone;

            try
            {
                indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "India Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "Asia/Kolkata");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, indiaTimeZone).Date;
        }

        private async Task<bool> IsEventAttendanceActiveAsync(int eventId)
        {
            var dates = await _attendanceDAL.GetEventDatesAsync(eventId);

            if (dates == null)
                return false;

            DateTime today = GetIndiaDate();

            DateTime startDate = dates.Value.StartDate.Date;
            DateTime endDate = dates.Value.EndDate.Date;

            // Reject events whose dates were never supplied.
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return false;

            return today >= startDate && today <= endDate;
        }


        // POST: api/home/attendance/send-otp
        [HttpPost("attendance/send-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> SendAttendanceOtp(
            [FromBody] AttendanceSendOtpDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (!await IsEventAttendanceActiveAsync(dto.EventId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Attendance is not available for this event today."
                });
            }

            // Check registration for this exact event before sending OTP.
            var participant = await _registrationDAL
                .GetRegisteredParticipantAsync(dto.EventId, dto.Email);

            if (participant == null)
            {
                return BadRequest(new
                {
                    success = false,
                    registered = false,
                    message = "You are not registered for this event. Please register first."
                });
            }

            // Generate a cryptographically secure six-digit OTP.
            string otp = RandomNumberGenerator.GetInt32(100000, 1000000)
                .ToString();

            string otpHash = HashOtp(otp);

            bool saved = await _attendanceDAL.SaveOtpAsync(
                dto.EventId,
                participant.ParticipantId,
                participant.Email,
                otpHash);

            if (!saved)
            {
                return StatusCode(429, new
                {
                    success = false,
                    message = "OTP request limit reached. Please try again after 30 minutes."
                });
            }

            var emailResult = await _emailService.SendAttendanceOtpAsync(
                participant.FullName,
                participant.Email,
                otp,
                cancellationToken);

            if (!emailResult.Success)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to send attendance OTP. Please try again."
                });
            }

            return Ok(new
            {
                success = true,
                registered = true,
                message = "OTP sent successfully to your registered email address.",
                email = participant.Email
            });
        }


        // POST: api/home/attendance/verify-otp
        [HttpPost("attendance/verify-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyAttendanceOtp([FromBody] AttendanceVerifyOtpDto dto)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (!await IsEventAttendanceActiveAsync(dto.EventId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Attendance is not available for this event today."
                });
            }

            // Recheck registration. Never trust an email supplied by the browser.
            var participant = await _registrationDAL.GetRegisteredParticipantAsync(dto.EventId, dto.Email);

            if (participant == null)
            {
                return BadRequest(new
                {
                    success = false,
                    registered = false,
                    message = "You are not registered for this event. Please register first."
                });
            }

            string otpHash = HashOtp(dto.OTP);

            string result = await _attendanceDAL.VerifyOtpAndMarkAttendanceAsync(
                    dto.EventId,
                    participant.ParticipantId,
                    participant.Email,
                    otpHash,
                    GetIndiaDate());

            switch (result)
            {
                case "SUCCESS":
                    return Ok(new
                    {
                        success = true,
                        attendanceMarked = true,
                        message = "Your attendance has been marked successfully.",
                        eventId = dto.EventId,
                        participantId = participant.ParticipantId,
                        participantName = participant.FullName,
                        attendanceDate = GetIndiaDate().ToString("yyyy-MM-dd")
                    });

                case "ALREADY_MARKED":
                    return Conflict(new
                    {
                        success = false,
                        attendanceMarked = false,
                        message = "Your attendance is already marked for today."
                    });

                case "INVALID":
                    return BadRequest(new
                    {
                        success = false,
                        message = "Incorrect OTP. Please try again."
                    });

                case "EXPIRED":
                    return BadRequest(new
                    {
                        success = false,
                        message = "OTP has expired. Please request a new OTP."
                    });

                case "MAX_ATTEMPTS":
                    return BadRequest(new
                    {
                        success = false,
                        message = "Maximum OTP attempts reached. Please request a new OTP."
                    });

                default:
                    return BadRequest(new
                    {
                        success = false,
                        message = "OTP not found. Please request a new OTP."
                    });
            }
        }

    }
}
