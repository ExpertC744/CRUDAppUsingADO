using CRUDAppUsingADO.Models.Attendance;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;

namespace CRUDAppUsingADO.DataAccess
{
    public sealed class AdminAttendanceDAL
    {
        private readonly string _connectionString;

        public AdminAttendanceDAL(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is missing.");
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // -----------------------------------------
        // 1. Get events belonging to logged-in admin
        // -----------------------------------------
        public async Task<List<AdminAttendanceEventDto>>
            GetAdminEventsAsync(Guid adminUserId)
        {
            var events = new List<AdminAttendanceEventDto>();

            const string sql = @"
                SELECT
                    EventId,
                    EventTitle,
                    EventStartDate,
                    EventCompletionDate
                FROM dbo.AdminEvents
                WHERE AdminUserId = @AdminUserId
                  AND nBit = 1
                  AND nSABit = 1
                ORDER BY EventStartDate DESC;";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add(
                "@AdminUserId", SqlDbType.UniqueIdentifier).Value =
                adminUserId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                events.Add(new AdminAttendanceEventDto
                {
                    EventId = Convert.ToInt32(reader["EventId"]),
                    EventTitle = reader["EventTitle"]?.ToString() ?? "",
                    EventStartDate =
                        Convert.ToDateTime(reader["EventStartDate"]),
                    EventCompletionDate =
                        Convert.ToDateTime(reader["EventCompletionDate"])
                });
            }

            return events;
        }

        // -----------------------------------------
        // 2. Attendance report for selected date
        // -----------------------------------------
        public async Task<AdminAttendanceReportDto?>
            GetAttendanceReportAsync(
                Guid adminUserId,
                int eventId,
                DateTime attendanceDate)
        {
            const string sql = @"
                SELECT
                    e.EventId,
                    e.EventTitle,
                    e.EventStartDate,
                    e.EventCompletionDate,

                    r.RegistrationId,
                    p.ParticipantId,
                    p.FullName,
                    p.Email,
                    p.WhatsAppNo,

                    a.AttendanceDate,
                    a.MarkedAtUtc

                FROM dbo.AdminEvents e

                INNER JOIN dbo.EventRegistrations r
                    ON r.EventId = e.EventId

                INNER JOIN dbo.EventRegistrationParticipants p
                    ON p.RegistrationId = r.RegistrationId

                LEFT JOIN dbo.EventAttendances a
                    ON a.EventId = e.EventId
                    AND a.ParticipantId = p.ParticipantId
                    AND a.AttendanceDate = @AttendanceDate

                WHERE e.EventId = @EventId
                  AND e.AdminUserId = @AdminUserId
                  AND e.nBit = 1
                  AND e.nSABit = 1

                ORDER BY r.RegistrationId, p.ParticipantId;";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier).Value = adminUserId;
            cmd.Parameters.Add("@AttendanceDate", SqlDbType.Date).Value = attendanceDate.Date;
            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            AdminAttendanceReportDto? report = null;
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            while (await reader.ReadAsync())
            {
                if (report == null)
                {
                    report = new AdminAttendanceReportDto
                    {
                        EventId = Convert.ToInt32(reader["EventId"]),
                        EventTitle = reader["EventTitle"]?.ToString() ?? "",
                        AttendanceDate = attendanceDate.Date,
                        EventStartDate = Convert.ToDateTime(reader["EventStartDate"]),
                        EventCompletionDate = Convert.ToDateTime(reader["EventCompletionDate"]),
                    };
                }

                bool isPresent = reader["AttendanceDate"] != DBNull.Value;

                string? attendanceTime = null;

                if (isPresent && reader["MarkedAtUtc"] != DBNull.Value)
                {
                    DateTime utcTime = DateTime.SpecifyKind(
                        Convert.ToDateTime(reader["MarkedAtUtc"]),
                        DateTimeKind.Utc);

                    DateTime istTime =
                        TimeZoneInfo.ConvertTimeFromUtc(utcTime, istZone);

                    attendanceTime = istTime.ToString(
                        "dd-MM-yyyy hh:mm tt",
                        CultureInfo.InvariantCulture);
                }

                report.Participants.Add(new AdminAttendanceParticipantDto
                    {
                        RegistrationId = Convert.ToInt32(reader["RegistrationId"]),
                        ParticipantId = Convert.ToInt32(reader["ParticipantId"]),
                        ParticipantName = reader["FullName"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        Mobile = reader["WhatsAppNo"]?.ToString() ?? "",
                        AttendanceStatus = isPresent ? "Present" : "Absent",
                        AttendanceTimeIST = attendanceTime

                    });
            }

            if (report == null)
                return null;

            report.TotalRegistered = report.Participants.Count;
            report.Present = report.Participants.Count(x => x.AttendanceStatus == "Present");
            report.Absent = report.TotalRegistered - report.Present;

            return report;
        }
    }
}