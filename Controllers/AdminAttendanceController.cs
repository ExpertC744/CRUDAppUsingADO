
using CRUDAppUsingADO.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/admin/attendance")]
    [Authorize(Roles = "Admin")]
    public class AdminAttendanceController : ControllerBase
    {
        private readonly AdminAttendanceDAL _attendanceDAL;

        public AdminAttendanceController(
            AdminAttendanceDAL attendanceDAL)
        {
            _attendanceDAL = attendanceDAL;
        }

        // Get logged-in AdminUserId from JWT
        private bool TryGetAdminUserId(out Guid adminUserId)
        {
            adminUserId = Guid.Empty;

            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            return Guid.TryParse(userId, out adminUserId);
        }

        // ==========================================
        // 1. GET ADMIN EVENTS FOR DROPDOWN
        // GET: api/admin/attendance/events
        // ==========================================
        [HttpGet("events")]
        public async Task<IActionResult> GetEvents()
        {
            if (!TryGetAdminUserId(out Guid adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid admin token."
                });
            }

            var events =
                await _attendanceDAL.GetAdminEventsAsync(adminUserId);

            return Ok(new
            {
                success = true,
                count = events.Count,
                data = events
            });
        }

        // ==========================================
        // 2. GET ATTENDANCE REPORT BY EVENT AND DATE
        // GET: api/admin/attendance/report
        // ==========================================
        [HttpGet("report")]
        public async Task<IActionResult> GetAttendanceReport(
            [FromQuery] int eventId,
            [FromQuery] DateTime date)
        {
            // Validate logged-in admin
            if (!TryGetAdminUserId(out Guid adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid admin token."
                });
            }

            // Validate EventId
            if (eventId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Valid Event ID is required."
                });
            }

            // Validate date
            if (date == DateTime.MinValue)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Valid attendance date is required."
                });
            }

            // Get attendance report
            var report =
                await _attendanceDAL.GetAttendanceReportAsync(
                    adminUserId,
                    eventId,
                    date.Date);

            // Event not found or admin does not own this event
            if (report == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Event not found, no participants registered, or you do not have access to this event."
                });
            }

            // ==========================================
            // IMPORTANT: VALIDATE EVENT DATE RANGE HERE
            // ==========================================

            if (date.Date < report.EventStartDate.Date ||
                date.Date > report.EventCompletionDate.Date)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Selected date is outside the event date range.",
                    eventStartDate = report.EventStartDate.ToString("yyyy-MM-dd"),
                    eventCompletionDate = report.EventCompletionDate.ToString("yyyy-MM-dd"),
                    selectedDate = date.ToString("yyyy-MM-dd")
                });
            }

            // ==========================================
            // RETURN ATTENDANCE REPORT
            // ==========================================

            return Ok(new
            {
                success = true,
                message = "Attendance report fetched successfully.",
                data = report
            });
        }
    }
}
