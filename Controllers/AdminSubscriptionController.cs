using System.Security.Claims;
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using CRUDAppUsingADO.Models.Configuration;
using CRUDAppUsingADO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/admin/subscription")]
    [Authorize(Roles = "Admin")]
    public class AdminSubscriptionController : ControllerBase
    {
        private readonly SubscriptionDAL _planDal;
        private readonly SubscriptionPaymentDAL _paymentDal;
        private readonly IRazorpayService _razorpay;
        private readonly PaymentGatewaySettings _settings;

        public AdminSubscriptionController(
            SubscriptionDAL planDal,
            SubscriptionPaymentDAL paymentDal,
            IRazorpayService razorpay,
            IOptions<PaymentGatewaySettings> options)
        {
            _planDal = planDal;
            _paymentDal = paymentDal;
            _razorpay = razorpay;
            _settings = options.Value;
        }

        private bool TryGetAdminId(out Guid adminId)
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            return Guid.TryParse(value, out adminId);
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _planDal.GetPlansAsync(true);

            return Ok(new ApiResponse<List<SubscriptionPlanDto>>
            {
                Success = true,
                Message = "Plans retrieved successfully.",
                Data = plans
            });
        }

        [HttpGet("plans/{id:int}")]
        public async Task<IActionResult> GetPlan(int id)
        {
            var plan = await _planDal.GetPlanAsync(id);

            if (plan == null || !plan.IsActive)
                return NotFound(new { success = false, message = "Plan not found." });

            return Ok(new ApiResponse<SubscriptionPlanDto>
            {
                Success = true,
                Message = "Plan retrieved successfully.",
                Data = plan
            });
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(
            [FromBody] CheckoutRequestDto request)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized(new { message = "Invalid Admin token." });

            if (request.PlanId <= 0)
                return BadRequest(new { message = "Please select a valid plan." });

            try
            {
                var result = await _paymentDal.CreateCheckoutAsync(
                    adminId,
                    request.PlanId,
                    request.PromoCode,
                    _razorpay);

                // The public KeyId is required by Razorpay Checkout.
                result.RazorpayKeyId = _settings.KeyId;

                return Ok(new ApiResponse<CheckoutResponseDto>
                {
                    Success = true,
                    Message = "Checkout order created successfully.",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("payments/verify")]
        public async Task<IActionResult> VerifyPayment(
            [FromBody] VerifyPaymentDto request)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized(new { message = "Invalid Admin token." });

            if (string.IsNullOrWhiteSpace(request.RazorpayOrderId) ||
                string.IsNullOrWhiteSpace(request.RazorpayPaymentId) ||
                string.IsNullOrWhiteSpace(request.RazorpaySignature))
            {
                return BadRequest(new { message = "Payment details are incomplete." });
            }

            try
            {
                var result = await _paymentDal.VerifyAndActivateAsync(
                    adminId, request, _razorpay);

                return Ok(new ApiResponse<PaymentVerificationResult>
                {
                    Success = true,
                    Message = "Payment verified successfully.",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetSubscriptionHistory()
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized();

            var history = await _paymentDal.GetSubscriptionHistoryAsync(adminId);

            return Ok(new ApiResponse<List<SubscriptionHistoryDto>>
            {
                Success = true,
                Message = "Subscription history retrieved.",
                Data = history
            });
        }

        [HttpGet("payments")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized();

            var payments = await _paymentDal.GetPaymentHistoryAsync(adminId);

            return Ok(new ApiResponse<List<PaymentHistoryDto>>
            {
                Success = true,
                Message = "Payment history retrieved.",
                Data = payments
            });
        }

        [HttpPost("calculate")]
        public async Task<IActionResult> CalculatePrice(
    [FromBody] CheckoutRequestDto request)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized(new { message = "Invalid Admin token." });

            if (request.PlanId <= 0)
                return BadRequest(new { message = "Please select a valid plan." });

            try
            {
                var result = await _planDal.CalculatePriceAsync(
                    request.PlanId,
                    request.PromoCode,
                    adminId);

                return Ok(new ApiResponse<PriceCalculationDto>
                {
                    Success = true,
                    Message = "Price calculated successfully. GST is 18%.",
                    Data = result
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
        }
    }
}