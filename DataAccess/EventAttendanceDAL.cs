
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public sealed class EventAttendanceDAL
{
    private readonly IConfiguration _configuration;

    public EventAttendanceDAL(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        return new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
    }

    public async Task<(DateTime StartDate, DateTime EndDate)?> GetEventDatesAsync(
        int eventId)
    {
        using var con = GetConnection();

        const string sql = @"
            SELECT EventStartDate, EventCompletionDate
            FROM dbo.AdminEvents
            WHERE EventId = @EventId
              AND nBit = 1
              AND nSABit = 1;";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return (
            Convert.ToDateTime(reader["EventStartDate"]),
            Convert.ToDateTime(reader["EventCompletionDate"])
        );
    }

    // Maximum 3 OTP requests in 30 minutes.
    public async Task<bool> SaveOtpAsync(
        int eventId,
        int participantId,
        string email,
        string otpHash)
    {
        using var con = GetConnection();
        await con.OpenAsync();

        using var transaction = con.BeginTransaction();

        try
        {
            const string countSql = @"
                SELECT COUNT(*)
                FROM dbo.EventAttendanceOTPs
                WHERE EventId = @EventId
                  AND ParticipantId = @ParticipantId
                  AND CreatedAtUtc >= DATEADD(MINUTE, -30, SYSUTCDATETIME());";

            using (var countCmd = new SqlCommand(
                countSql, con, transaction))
            {
                countCmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
                countCmd.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;

                int count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                if (count >= 3)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            const string sql = @"
                UPDATE dbo.EventAttendanceOTPs
                SET IsUsed = 1
                WHERE EventId = @EventId
                  AND ParticipantId = @ParticipantId
                  AND IsUsed = 0;

                INSERT INTO dbo.EventAttendanceOTPs
                (
                    EventId, ParticipantId, Email,
                    OTPHash, ExpiresAtUtc, Attempts, IsUsed
                )
                VALUES
                (
                    @EventId, @ParticipantId, @Email,
                    @OTPHash, DATEADD(MINUTE, 5, SYSUTCDATETIME()), 0, 0
                );";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
                cmd.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = email;
                cmd.Parameters.Add("@OTPHash", SqlDbType.NVarChar, 64).Value = otpHash;

                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Returns: SUCCESS, NOT_FOUND, EXPIRED, INVALID, MAX_ATTEMPTS, ALREADY_MARKED
    public async Task<string> VerifyOtpAndMarkAttendanceAsync(
        int eventId,
        int participantId,
        string email,
        string otpHash,
        DateTime attendanceDate)
    {
        using var con = GetConnection();
        await con.OpenAsync();

        using var transaction = con.BeginTransaction();

        try
        {
            const string selectSql = @"
                SELECT TOP 1 OTPId, OTPHash, ExpiresAtUtc, Attempts
                FROM dbo.EventAttendanceOTPs WITH (UPDLOCK, ROWLOCK)
                WHERE EventId = @EventId
                  AND ParticipantId = @ParticipantId
                  AND Email = @Email
                  AND IsUsed = 0
                ORDER BY CreatedAtUtc DESC, OTPId DESC;";

            long otpId;
            string savedHash;
            DateTime expiresAt;
            int attempts;

            using (var cmd = new SqlCommand(selectSql, con, transaction))
            {
                cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
                cmd.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = email;

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    await reader.CloseAsync();
                    await transaction.RollbackAsync();
                    return "NOT_FOUND";
                }

                otpId = Convert.ToInt64(reader["OTPId"]);
                savedHash = reader["OTPHash"].ToString()!;
                expiresAt = Convert.ToDateTime(reader["ExpiresAtUtc"]);
                attempts = Convert.ToInt32(reader["Attempts"]);
            }

            if (attempts >= 5)
            {
                await transaction.RollbackAsync();
                return "MAX_ATTEMPTS";
            }

            if (expiresAt <= DateTime.UtcNow)
            {
                await transaction.RollbackAsync();
                return "EXPIRED";
            }

            if (!string.Equals(savedHash, otpHash, StringComparison.Ordinal))
            {
                const string invalidSql = @"
                    UPDATE dbo.EventAttendanceOTPs
                    SET Attempts = Attempts + 1,
                        IsUsed = CASE WHEN Attempts + 1 >= 5 THEN 1 ELSE 0 END
                    WHERE OTPId = @OTPId;";

                using var invalidCmd = new SqlCommand(
                    invalidSql, con, transaction);

                invalidCmd.Parameters.Add("@OTPId", SqlDbType.BigInt).Value = otpId;

                await invalidCmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                return attempts + 1 >= 5 ? "MAX_ATTEMPTS" : "INVALID";
            }

            const string markOtpSql = @"
                UPDATE dbo.EventAttendanceOTPs
                SET IsUsed = 1
                WHERE OTPId = @OTPId;";

            using (var markCmd = new SqlCommand(
                markOtpSql, con, transaction))
            {
                markCmd.Parameters.Add("@OTPId", SqlDbType.BigInt).Value = otpId;
                await markCmd.ExecuteNonQueryAsync();
            }

            const string attendanceSql = @"
                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.EventAttendances
                    WHERE EventId = @EventId
                      AND ParticipantId = @ParticipantId
                      AND AttendanceDate = @AttendanceDate
                )
                BEGIN
                    SELECT 0;
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.EventAttendances
                    (
                        EventId, ParticipantId, Email, AttendanceDate
                    )
                    VALUES
                    (
                        @EventId, @ParticipantId, @Email, @AttendanceDate
                    );

                    SELECT 1;
                END;";

            int inserted;

            using (var attendanceCmd = new SqlCommand(
                attendanceSql, con, transaction))
            {
                attendanceCmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
                attendanceCmd.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
                attendanceCmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = email;
                attendanceCmd.Parameters.Add("@AttendanceDate", SqlDbType.Date).Value = attendanceDate.Date;

                inserted = Convert.ToInt32(
                    await attendanceCmd.ExecuteScalarAsync());
            }

            await transaction.CommitAsync();

            return inserted == 1 ? "SUCCESS" : "ALREADY_MARKED";
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
