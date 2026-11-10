
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
        private readonly AdminEventDAL _adminEventDAL;
        private readonly TemplateManagementDAL _templateDAL;
        private readonly SubscriptionDAL _subscriptionDAL;
        private readonly PromoCodeDAL _promoCodeDAL;

        public SuperAdminController(
    FeedbackDAL feedbackDAL,
    AdminProfileDAL adminProfileDAL,
    AdminEventDAL adminEventDAL,
    TemplateManagementDAL templateDAL,
    SubscriptionDAL subscriptionDAL,
    PromoCodeDAL promoCodeDAL)
        {
            _feedbackDAL = feedbackDAL;
            _adminProfileDAL = adminProfileDAL;
            _adminEventDAL = adminEventDAL;
            _templateDAL = templateDAL;
            _subscriptionDAL = subscriptionDAL;
            _promoCodeDAL = promoCodeDAL;
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
            var feedbackList = await _feedbackDAL.GetAllFeedbackForSuperAdminAsync();

            return Ok(new
            {
                success = true,
                count = feedbackList.Count,
                feedback = feedbackList
            });
        }

        // PATCH: api/superadmin/feedback/5/status
        [HttpPatch("feedback/{feedbackId:int}/status")]
        public async Task<IActionResult> SetFeedbackStatus(int feedbackId, [FromBody] FeedbackStatusDto dto)
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

            bool updated = await _feedbackDAL.SetFeedbackStatusAsync(feedbackId, dto.nBit);

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
        public async Task<IActionResult> UpdateAdminStatus(Guid userId, [FromBody] AdminStatusDto dto)
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

        // PATCH: api/superadmin/events/5/approval
        [HttpPatch("events/{eventId:int}/approval")]
        public async Task<IActionResult> UpdateEventApproval(int eventId, [FromBody] AdminEventApprovalDto dto)
        {
            if (eventId <= 0)
                return BadRequest(new { message = "Invalid Event ID." });

            bool updated = await _adminEventDAL.UpdateSuperAdminStatusAsync(
                eventId,
                dto.nSABit);

            if (!updated)
                return NotFound(new { message = "Event not found." });

            return Ok(new
            {
                success = true,
                eventId,
                nSABit = dto.nSABit,
                message = dto.nSABit
                    ? "Event enabled by SuperAdmin."
                    : "Event disabled by SuperAdmin."
            });
        }



        // ============================================
        // TEMPLATE ADMIN MANAGEMENT
        // ============================================

        // POST: api/superadmin/template-admins
        [HttpPost("template-admins")]
        public async Task<IActionResult> InsertTemplateAdmin(
            [FromBody] TemplateAdminDto dto)
        {
            try
            {
                int id = await _templateDAL.InsertTemplateAdminAsync(dto);

                return Ok(new
                {
                    success = true,
                    templateAdminId = id,
                    message = "Template Admin added successfully."
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(new
                {
                    success = false,
                    message = "Email already exists."
                });
            }
        }

        // PUT: api/superadmin/template-admins/5
        [HttpPut("template-admins/{id:int}")]
        public async Task<IActionResult> UpdateTemplateAdmin(
            int id, [FromBody] TemplateAdminDto dto)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid Template Admin ID." });

            try
            {
                bool updated = await _templateDAL.UpdateTemplateAdminAsync(id, dto);

                if (!updated)
                    return NotFound(new { message = "Template Admin not found." });

                return Ok(new
                {
                    success = true,
                    message = "Template Admin updated successfully."
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(new { message = "Email already exists." });
            }
        }

        // GET: api/superadmin/template-admins/5
        [HttpGet("template-admins/{id:int}")]
        public async Task<IActionResult> GetTemplateAdminById(int id)
        {
            var admin = await _templateDAL.GetTemplateAdminByIdAsync(id);

            if (admin == null)
                return NotFound(new { message = "Template Admin not found." });

            return Ok(new { success = true, data = admin });
        }

        // GET: api/superadmin/template-admins
        [HttpGet("template-admins")]
        public async Task<IActionResult> GetAllTemplateAdmins()
        {
            var admins = await _templateDAL.GetAllTemplateAdminsAsync();

            return Ok(new
            {
                success = true,
                totalRecords = admins.Count,
                data = admins
            });
        }

        // PATCH: api/superadmin/template-admins/5/status
        [HttpPatch("template-admins/{id:int}/status")]
        public async Task<IActionResult> UpdateTemplateAdminStatus(
            int id, [FromBody] TemplateStatusDto dto)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid Template Admin ID." });

            bool updated = await _templateDAL.UpdateTemplateAdminStatusAsync(
                id, dto.nBit);

            if (!updated)
                return NotFound(new { message = "Template Admin not found." });

            return Ok(new
            {
                success = true,
                templateAdminId = id,
                nBit = dto.nBit,
                message = dto.nBit
                    ? "Template Admin enabled successfully."
                    : "Template Admin disabled successfully."
            });
        }




        // ============================================
        // TEMPLATE MANAGEMENT
        // ============================================

        // POST: api/superadmin/templates
        [HttpPost("templates")]
        public async Task<IActionResult> InsertTemplate(
            [FromBody] TemplateDto dto)
        {
            try
            {
                int id = await _templateDAL.InsertTemplateAsync(dto);

                var template = await _templateDAL.GetTemplateByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    templateId = id,
                    message = "Template added successfully.",
                    data = template
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 547)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Template Admin ID. Please select a valid Template Admin."
                });
            }
        }

        // PUT: api/superadmin/templates/5
        [HttpPut("templates/{id:int}")]
        public async Task<IActionResult> UpdateTemplate(
            int id, [FromBody] TemplateDto dto)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid Template ID." });

            try
            {
                bool updated = await _templateDAL.UpdateTemplateAsync(id, dto);

                if (!updated)
                    return NotFound(new { message = "Template not found." });

                var template = await _templateDAL.GetTemplateByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Template updated successfully.",
                    data = template
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 547)
            {
                return BadRequest(new
                {
                    message = "Invalid Template Admin ID."
                });
            }
        }

        // GET: api/superadmin/templates/5
        [HttpGet("templates/{id:int}")]
        public async Task<IActionResult> GetTemplateById(int id)
        {
            var template = await _templateDAL.GetTemplateByIdAsync(id);

            if (template == null)
                return NotFound(new { message = "Template not found." });

            return Ok(new { success = true, data = template });
        }

        // GET: api/superadmin/templates
        [HttpGet("templates")]
        public async Task<IActionResult> GetAllTemplates()
        {
            var templates = await _templateDAL.GetAllTemplatesAsync();

            return Ok(new
            {
                success = true,
                totalRecords = templates.Count,
                data = templates
            });
        }

        // PATCH: api/superadmin/templates/5/status
        [HttpPatch("templates/{id:int}/status")]
        public async Task<IActionResult> UpdateTemplateStatus(
            int id, [FromBody] TemplateStatusDto dto)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid Template ID." });

            bool updated = await _templateDAL.UpdateTemplateStatusAsync(
                id, dto.nBit);

            if (!updated)
                return NotFound(new { message = "Template not found." });

            return Ok(new
            {
                success = true,
                templateId = id,
                nBit = dto.nBit,
                message = dto.nBit
                    ? "Template enabled successfully."
                    : "Template disabled successfully."
            });
        }


        // ============================================
        // SUBSCRIPTION PLAN MANAGEMENT
        // ============================================

        // POST: api/superadmin/plans
        [HttpPost("plans")]
        public async Task<IActionResult> CreatePlan([FromBody] CreatePlanDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(dto.PlanName))
                return BadRequest(new { success = false, message = "Plan name is required." });

            if (dto.PlanCategory is < 1 or > 4)
                return BadRequest(new { success = false, message = "Invalid plan category." });

            if (dto.DurationValue <= 0 || dto.DurationDays <= 0)
                return BadRequest(new { success = false, message = "Duration must be greater than zero." });

            if (dto.Price < 0 || dto.TaxPercentage < 0 || dto.TaxPercentage > 100)
                return BadRequest(new { success = false, message = "Invalid price or tax percentage." });

            if (string.IsNullOrWhiteSpace(dto.Currency) || dto.Currency.Trim().Length != 3)
                return BadRequest(new { success = false, message = "Currency must be a three-character code, such as INR." });

            try
            {
                int id = await _subscriptionDAL.CreatePlanAsync(dto);
                var plan = await _subscriptionDAL.GetPlanAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Subscription plan created successfully.",
                    planId = id,
                    data = plan
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // GET: api/superadmin/plans
        [HttpGet("plans")]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _subscriptionDAL.GetPlansAsync(false);

            return Ok(new
            {
                success = true,
                totalRecords = plans.Count,
                data = plans
            });
        }

        // GET: api/superadmin/plans/5
        [HttpGet("plans/{id:int}")]
        public async Task<IActionResult> GetPlanById(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid plan ID." });

            var plan = await _subscriptionDAL.GetPlanAsync(id);

            if (plan == null)
                return NotFound(new { success = false, message = "Plan not found." });

            return Ok(new { success = true, data = plan });
        }

        // PUT: api/superadmin/plans/5
        [HttpPut("plans/{id:int}")]
        public async Task<IActionResult> UpdatePlan(
            int id, [FromBody] CreatePlanDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid plan ID." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.PlanName))
                return BadRequest(new { success = false, message = "Valid plan details are required." });

            if (dto.PlanCategory is < 1 or > 4 ||
                dto.DurationValue <= 0 || dto.DurationDays <= 0 ||
                dto.Price < 0 || dto.TaxPercentage < 0 ||
                dto.TaxPercentage > 100 ||
                string.IsNullOrWhiteSpace(dto.Currency) ||
                dto.Currency.Trim().Length != 3)
            {
                return BadRequest(new { success = false, message = "Please check the plan details." });
            }

            bool updated = await _subscriptionDAL.UpdatePlanAsync(id, dto);

            if (!updated)
                return NotFound(new { success = false, message = "Plan not found." });

            var plan = await _subscriptionDAL.GetPlanAsync(id);

            return Ok(new
            {
                success = true,
                message = "Subscription plan updated successfully.",
                data = plan
            });
        }

        // PATCH: api/superadmin/plans/5/status
        [HttpPatch("plans/{id:int}/status")]
        public async Task<IActionResult> UpdatePlanStatus(
            int id, [FromBody] SubscriptionStatusDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid plan ID." });

            if (dto == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            bool updated = await _subscriptionDAL.UpdatePlanStatusAsync(
                id, dto.IsActive);

            if (!updated)
                return NotFound(new { success = false, message = "Plan not found." });

            return Ok(new
            {
                success = true,
                planId = id,
                isActive = dto.IsActive,
                message = dto.IsActive
                    ? "Plan enabled successfully."
                    : "Plan disabled successfully."
            });
        }


        // ============================================
        // PROMO CODE MANAGEMENT
        // ============================================

        // POST: api/superadmin/promocodes
        [HttpPost("promocodes")]
        public async Task<IActionResult> CreatePromoCode(
            [FromBody] CreatePromoCodeDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            try
            {
                int id = await _promoCodeDAL.CreateAsync(dto);
                var promo = await _promoCodeDAL.GetByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Promo code created successfully.",
                    promoCodeId = id,
                    data = promo
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(new
                {
                    success = false,
                    message = "This promo code already exists."
                });
            }
        }

        // GET: api/superadmin/promocodes
        // GET: api/superadmin/promocodes
        [HttpGet("promocodes")]
        public async Task<IActionResult> GetAllPromoCodes()
        {
            var promos = await _promoCodeDAL.GetAllAsync();

            var plans = await _subscriptionDAL.GetPlansAsync(false);

            foreach (var promo in promos)
            {
                promo.PlanDetails = plans
                    .Where(p => promo.PlanIds.Contains(p.PlanId))
                    .ToList();
            }

            return Ok(new
            {
                success = true,
                totalRecords = promos.Count,
                data = promos
            });
        }

        // GET: api/superadmin/promocodes/5
        [HttpGet("promocodes/{id:int}")]
        public async Task<IActionResult> GetPromoCodeById(int id)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid promo code ID."
                });

            var promo = await _promoCodeDAL.GetByIdAsync(id);

            if (promo == null)
                return NotFound(new
                {
                    success = false,
                    message = "Promo code not found."
                });

            var plans = await _subscriptionDAL.GetPlansAsync(false);

            promo.PlanDetails = plans
                .Where(p => promo.PlanIds.Contains(p.PlanId))
                .ToList();

            return Ok(new
            {
                success = true,
                data = promo
            });
        }

        // PUT: api/superadmin/promocodes/5
        [HttpPut("promocodes/{id:int}")]
        public async Task<IActionResult> UpdatePromoCode(
            int id, [FromBody] CreatePromoCodeDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid promo code ID." });

            if (dto == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            try
            {
                bool updated = await _promoCodeDAL.UpdateAsync(id, dto);

                if (!updated)
                    return NotFound(new { success = false, message = "Promo code not found." });

                var promo = await _promoCodeDAL.GetByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Promo code updated successfully.",
                    data = promo
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(new
                {
                    success = false,
                    message = "This promo code already exists."
                });
            }
        }

        // PATCH: api/superadmin/promocodes/5/status
        [HttpPatch("promocodes/{id:int}/status")]
        public async Task<IActionResult> UpdatePromoCodeStatus(
            int id, [FromBody] SubscriptionStatusDto dto)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid promo code ID." });

            if (dto == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            bool updated = await _promoCodeDAL.UpdateStatusAsync(
                id, dto.IsActive);

            if (!updated)
                return NotFound(new { success = false, message = "Promo code not found." });

            return Ok(new
            {
                success = true,
                promoCodeId = id,
                isActive = dto.IsActive,
                message = dto.IsActive
                    ? "Promo code enabled successfully."
                    : "Promo code disabled successfully."
            });
        }

        // GET: api/superadmin/promocodes/plans
        [HttpGet("promocodes/plans")]
        public async Task<IActionResult> GetPromoCodePlans()
        {
            var plans = await _subscriptionDAL.GetPlansAsync(false);

            var result = plans
                .Where(x => x.IsActive)
                .OrderBy(x => x.PlanCategory)
                .ThenBy(x => x.DisplayOrder)
                .Select(x => new
                {
                    planId = x.PlanId,
                    planName = x.PlanName,
                    planCategory = x.PlanCategory,
                    categoryName = x.PlanCategory switch
                    {
                        1 => "Events",
                        2 => "Creatives",
                        3 => "Events + Creatives",
                        4 => "Download",
                        _ => "Other"
                    },
                    durationValue = x.DurationValue,
                    durationUnit = x.DurationUnit,
                    durationDays = x.DurationDays,
                    price = x.Price,
                    displayName = $"{x.PlanName} - {x.DurationValue} {x.DurationUnit}"
                })
                .ToList();

            return Ok(new
            {
                success = true,
                totalRecords = result.Count,
                data = result
            });
        }
    }
}
