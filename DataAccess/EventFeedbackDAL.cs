using CRUDAppUsingADO.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess
{
    public class EventFeedbackDAL
    {
        private readonly IConfiguration _configuration;

        public EventFeedbackDAL(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        private static string NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return "";

            return new string(mobile.Where(char.IsDigit).ToArray());
        }

        // =========================================================
        // 1. SUBMIT FEEDBACK
        // Registered participant verification:
        // Email and WhatsApp/mobile must match the same event.
        // =========================================================
        public async Task<int> SubmitFeedbackAsync(
            EventFeedbackSubmitDto dto,
            CancellationToken ct)
        {
            using var con = GetConnection();

            await con.OpenAsync(ct);

            // Verify the participant registered for this specific event.
            const string participantSql = @"
                SELECT
                    p.FullName,
                    p.Email,
                    p.WhatsAppNo
                FROM dbo.EventRegistrations r
                INNER JOIN dbo.EventRegistrationParticipants p
                    ON p.RegistrationId = r.RegistrationId
                WHERE r.EventId = @EventId
                  AND LOWER(LTRIM(RTRIM(p.Email))) = @Email
                ORDER BY p.ParticipantId;";

            string registeredName = "";
            bool isRegistered = false;

            using (var cmd = new SqlCommand(participantSql, con))
            {
                cmd.Parameters.Add("@EventId", SqlDbType.Int)
                    .Value = dto.EventId;

                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                    .Value = dto.Email.Trim().ToLowerInvariant();

                using var reader = await cmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    var registeredMobile = reader["WhatsAppNo"] == DBNull.Value
                        ? ""
                        : reader["WhatsAppNo"].ToString();

                    if (NormalizeMobile(registeredMobile) ==
                        NormalizeMobile(dto.Mobile))
                    {
                        registeredName = reader["FullName"].ToString() ?? "";
                        isRegistered = true;
                        break;
                    }
                }
            }

            if (!isRegistered)
            {
                throw new InvalidOperationException(
                    "You must be registered for this event using the same email and mobile number to submit feedback.");
            }

            // Check if email or mobile has already submitted feedback
            // for this event.
            const string duplicateSql = @"
                SELECT COUNT(1)
                FROM dbo.EventFeedback
                WHERE EventId = @EventId
                  AND
                  (
                      LOWER(LTRIM(RTRIM(Email))) = @Email
                      OR Mobile = @Mobile
                  );";

            using (var duplicateCmd = new SqlCommand(duplicateSql, con))
            {
                duplicateCmd.Parameters.Add("@EventId", SqlDbType.Int)
                    .Value = dto.EventId;

                duplicateCmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                    .Value = dto.Email.Trim().ToLowerInvariant();

                duplicateCmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 20)
                    .Value = NormalizeMobile(dto.Mobile);

                int duplicateCount = Convert.ToInt32(
                    await duplicateCmd.ExecuteScalarAsync(ct));

                if (duplicateCount > 0)
                {
                    throw new InvalidOperationException(
                        "You have already submitted feedback for this event.");
                }
            }

            const string insertSql = @"
                INSERT INTO dbo.EventFeedback
                (
                    EventId,
                    FullName,
                    Email,
                    Mobile,
                    Feedback,
                    IsApproved
                )
                VALUES
                (
                    @EventId,
                    @FullName,
                    @Email,
                    @Mobile,
                    @Feedback,
                    0
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var insertCmd = new SqlCommand(insertSql, con);

            insertCmd.Parameters.Add("@EventId", SqlDbType.Int)
                .Value = dto.EventId;

            insertCmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150)
                .Value = dto.FullName.Trim();

            insertCmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                .Value = dto.Email.Trim().ToLowerInvariant();

            insertCmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 20)
                .Value = NormalizeMobile(dto.Mobile);

            insertCmd.Parameters.Add("@Feedback", SqlDbType.NVarChar, 1000)
                .Value = dto.Feedback.Trim();

            return Convert.ToInt32(
                await insertCmd.ExecuteScalarAsync(ct));
        }

        // =========================================================
        // 2. PUBLIC - GET ONLY APPROVED FEEDBACK
        // =========================================================
        public async Task<List<object>> GetApprovedFeedbackAsync(
            int eventId,
            CancellationToken ct)
        {
            var result = new List<object>();

            using var con = GetConnection();

            const string sql = @"
                SELECT
                    FeedbackId,
                    EventId,
                    FullName,
                    Feedback,
                    CreatedAt
                FROM dbo.EventFeedback
                WHERE EventId = @EventId
                  AND IsApproved = 1
                ORDER BY CreatedAt DESC;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int)
                .Value = eventId;

            await con.OpenAsync(ct);

            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                result.Add(new
                {
                    FeedbackId = Convert.ToInt32(reader["FeedbackId"]),
                    EventId = Convert.ToInt32(reader["EventId"]),
                    FullName = reader["FullName"].ToString(),
                    Feedback = reader["Feedback"].ToString(),
                    CreatedAt = reader["CreatedAt"]
                });
            }

            return result;
        }

        // =========================================================
        // 3. ADMIN - GET FEEDBACK FOR ADMIN'S OWN EVENT
        // =========================================================
        public async Task<List<object>> GetAdminEventFeedbackAsync(
            int eventId,
            Guid adminUserId,
            CancellationToken ct)
        {
            var result = new List<object>();

            using var con = GetConnection();

            const string sql = @"
                SELECT
                    f.FeedbackId,
                    f.EventId,
                    e.EventTitle,
                    f.FullName,
                    f.Email,
                    f.Mobile,
                    f.Feedback,
                    f.IsApproved,
                    f.CreatedAt,
                    f.UpdatedAt
                FROM dbo.EventFeedback f
                INNER JOIN dbo.AdminEvents e
                    ON e.EventId = f.EventId
                WHERE f.EventId = @EventId
                  AND e.AdminUserId = @AdminUserId
                ORDER BY f.CreatedAt DESC;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int)
                .Value = eventId;

            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier)
                .Value = adminUserId;

            await con.OpenAsync(ct);

            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                result.Add(new
                {
                    FeedbackId = Convert.ToInt32(reader["FeedbackId"]),
                    EventId = Convert.ToInt32(reader["EventId"]),
                    EventTitle = reader["EventTitle"].ToString(),
                    FullName = reader["FullName"].ToString(),
                    Email = reader["Email"].ToString(),
                    Mobile = reader["Mobile"].ToString(),
                    Feedback = reader["Feedback"].ToString(),
                    IsApproved = Convert.ToBoolean(reader["IsApproved"]),
                    CreatedAt = reader["CreatedAt"],
                    UpdatedAt = reader["UpdatedAt"] == DBNull.Value
                        ? null
                        : reader["UpdatedAt"]
                });
            }

            return result;
        }

        // =========================================================
        // 4. ADMIN - APPROVE / DISABLE FEEDBACK
        // Ownership is checked inside this SQL query.
        // =========================================================
        public async Task<bool> UpdateApprovalAsync(
            int eventId,
            int feedbackId,
            Guid adminUserId,
            bool isApproved,
            CancellationToken ct)
        {
            using var con = GetConnection();

            const string sql = @"
                UPDATE f
                SET
                    f.IsApproved = @IsApproved,
                    f.UpdatedAt = GETDATE()
                FROM dbo.EventFeedback f
                INNER JOIN dbo.AdminEvents e
                    ON e.EventId = f.EventId
                WHERE f.FeedbackId = @FeedbackId
                  AND f.EventId = @EventId
                  AND e.AdminUserId = @AdminUserId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@FeedbackId", SqlDbType.Int)
                .Value = feedbackId;

            cmd.Parameters.Add("@EventId", SqlDbType.Int)
                .Value = eventId;

            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier)
                .Value = adminUserId;

            cmd.Parameters.Add("@IsApproved", SqlDbType.Bit)
                .Value = isApproved;

            await con.OpenAsync(ct);

            int rows = await cmd.ExecuteNonQueryAsync(ct);

            return rows > 0;
        }
    }
}