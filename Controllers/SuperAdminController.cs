
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

        public SuperAdminController(FeedbackDAL feedbackDAL, AdminProfileDAL adminProfileDAL, AdminEventDAL adminEventDAL, TemplateManagementDAL templateDAL)
        {
            _feedbackDAL = feedbackDAL;
            _adminProfileDAL = adminProfileDAL;
            _adminEventDAL = adminEventDAL;
            _templateDAL = templateDAL;
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
    }
}
