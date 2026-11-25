using CRUDAppUsingADO.Models;
using CRUDAppUsingADO.Models.Attendance;
using CRUDAppUsingADO.Models.Bulk;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess
{
    public class EventRegistrationDAL
    {
        private readonly IConfiguration _configuration;

        public EventRegistrationDAL(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        }


        public async Task<int> SaveRegistrationAsync(EventRegistrationDto dto, List<string?> imagePaths, decimal entryFee)
        {
            if (dto.Participants == null || dto.Participants.Count == 0)
                throw new InvalidOperationException("At least one participant is required.");

            if (imagePaths.Count != dto.Participants.Count)
                throw new InvalidOperationException("Participant image count mismatch.");

            using var con = GetConnection();

            await con.OpenAsync();

            using var transaction = await con.BeginTransactionAsync();

            try
            {
                const string registrationSql = @"
            INSERT INTO dbo.EventRegistrations
            (
                EventId,
                RegistrationType,
                GroupName,
                TeamLeadName,
                TotalParticipants,
                TotalAmount
            )
            VALUES
            (
                @EventId,
                @RegistrationType,
                @GroupName,
                @TeamLeadName,
                @TotalParticipants,
                @TotalAmount
            );

            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int registrationId;

                using (var cmd = new SqlCommand(registrationSql, con, (SqlTransaction)transaction))
                {
                    cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = dto.EventId;

                    cmd.Parameters.Add("@RegistrationType", SqlDbType.NVarChar, 20).Value = dto.RegistrationType;

                    cmd.Parameters.Add("@GroupName", SqlDbType.NVarChar, 150).Value = (object?)dto.GroupName ?? DBNull.Value;

                    cmd.Parameters.Add("@TeamLeadName", SqlDbType.NVarChar, 150).Value = (object?)dto.TeamLeadName ?? DBNull.Value;

                    cmd.Parameters.Add("@TotalParticipants", SqlDbType.Int).Value = dto.Participants.Count;

                    var amount = cmd.Parameters.Add("@TotalAmount", SqlDbType.Decimal);

                    amount.Precision = 10;
                    amount.Scale = 2;
                    amount.Value = entryFee * dto.Participants.Count;

                    registrationId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }


                const string participantSql = @"
    INSERT INTO dbo.EventRegistrationParticipants
    (
        RegistrationId,
        FullName,
        Gender,
        DOB,
        Age,
        Email,
        WhatsAppNo,
        Address,
        ProfileImage,
        RegistrationCategory,
        CollegeName,
        Stream,
        Branch,
        AcademicYear,
        OrganizationName,
        Designation,
        Experience
    )
    VALUES
    (
        @RegistrationId,
        @FullName,
        @Gender,
        @DOB,
        @Age,
        @Email,
        @WhatsAppNo,
        @Address,
        @ProfileImage,
        @RegistrationCategory,
        @CollegeName,
        @Stream,
        @Branch,
        @AcademicYear,
        @OrganizationName,
        @Designation,
        @Experience
    );";


                // IMPORTANT: Insert all participants under ONE RegistrationId.

                for (int i = 0; i < dto.Participants.Count; i++)
                {
                    var p = dto.Participants[i];

                    using var cmd = new SqlCommand(
                        participantSql, con, (SqlTransaction)transaction);

                    cmd.Parameters.Add("@RegistrationId", SqlDbType.Int)
                        .Value = registrationId;

                    cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150)
                        .Value = p.FullName.Trim();

                    cmd.Parameters.Add("@Gender", SqlDbType.NVarChar, 20)
                        .Value = p.Gender.Trim();

                    cmd.Parameters.Add("@DOB", SqlDbType.Date)
                        .Value = p.DOB.Date;

                    cmd.Parameters.Add("@Age", SqlDbType.Int)
                        .Value = CalculateAge(p.DOB);

                    cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                        .Value = p.Email.Trim();

                    cmd.Parameters.Add("@WhatsAppNo", SqlDbType.NVarChar, 20)
                        .Value = p.WhatsAppNo.Trim();

                    cmd.Parameters.Add("@Address", SqlDbType.NVarChar, 500)
                        .Value = p.Address.Trim();

                    cmd.Parameters.Add("@ProfileImage", SqlDbType.NVarChar, 1000)
                        .Value = (object?)imagePaths[i] ?? DBNull.Value;

                    cmd.Parameters.Add("@RegistrationCategory", SqlDbType.NVarChar, 80)
                        .Value = p.RegistrationCategory;

                    // Student / Fresher fields
                    cmd.Parameters.Add("@CollegeName", SqlDbType.NVarChar, 200)
                        .Value = (object?)p.CollegeName?.Trim() ?? DBNull.Value;

                    cmd.Parameters.Add("@Stream", SqlDbType.NVarChar, 100)
                        .Value = (object?)p.Stream?.Trim() ?? DBNull.Value;

                    cmd.Parameters.Add("@Branch", SqlDbType.NVarChar, 150)
                        .Value = (object?)p.Branch?.Trim() ?? DBNull.Value;

                    cmd.Parameters.Add("@AcademicYear", SqlDbType.NVarChar, 100)
                        .Value = (object?)p.AcademicYear?.Trim() ?? DBNull.Value;

                    // Working Professional fields
                    cmd.Parameters.Add("@OrganizationName", SqlDbType.NVarChar, 200)
                        .Value = (object?)p.OrganizationName?.Trim() ?? DBNull.Value;

                    cmd.Parameters.Add("@Designation", SqlDbType.NVarChar, 100)
                        .Value = (object?)p.Designation?.Trim() ?? DBNull.Value;

                    cmd.Parameters.Add("@Experience", SqlDbType.NVarChar, 50)
                        .Value = (object?)p.Experience?.Trim() ?? DBNull.Value;

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return registrationId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;

            int age = today.Year - dob.Year;

            if (dob.Date > today.AddYears(-age))
                age--;

            return age;
        }

        public async Task<List<object>> GetEventRegistrationsAsync(int eventId)
        {
            var result = new List<object>();
            using var con = GetConnection();
            const string sql = @"
                SELECT
                    r.RegistrationId,
                    r.EventId,
                    r.RegistrationType,
                    r.GroupName,
                    r.TeamLeadName,
                    r.TotalParticipants,
                    r.TotalAmount,
                    r.RegistrationDate,
                    r.RegistrationStatus,

                    p.ParticipantId,
                    p.FullName,
                    p.Gender,
                    p.DOB,
                    p.Age,
                    p.Email,
                    p.WhatsAppNo,
                    p.Address,
                    p.ProfileImage,
                    p.RegistrationCategory,
                    p.CollegeName,
                    p.Stream,
                    p.Branch,
                    p.AcademicYear,
                    p.OrganizationName,
                    p.Designation,
                    p.Experience

                FROM dbo.EventRegistrations r

                INNER JOIN dbo.EventRegistrationParticipants p
                    ON r.RegistrationId = p.RegistrationId

                WHERE r.EventId = @EventId

                ORDER BY r.RegistrationDate DESC, p.ParticipantId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    RegistrationId = reader["RegistrationId"],
                    EventId = reader["EventId"],
                    RegistrationType = reader["RegistrationType"],
                    GroupName = reader["GroupName"],
                    TeamLeadName = reader["TeamLeadName"],
                    TotalParticipants = reader["TotalParticipants"],
                    TotalAmount = reader["TotalAmount"],
                    RegistrationDate = reader["RegistrationDate"],
                    RegistrationStatus = reader["RegistrationStatus"],

                    ParticipantId = reader["ParticipantId"],
                    FullName = reader["FullName"],
                    Gender = reader["Gender"],
                    DOB = reader["DOB"],
                    Age = reader["Age"],
                    Email = reader["Email"],
                    WhatsAppNo = reader["WhatsAppNo"],
                    Address = reader["Address"],
                    ProfileImage = reader["ProfileImage"],
                    RegistrationCategory = reader["RegistrationCategory"],
                    CollegeName = reader["CollegeName"],
                    Stream = reader["Stream"],
                    Branch = reader["Branch"],
                    AcademicYear = reader["AcademicYear"],
                    OrganizationName = reader["OrganizationName"] == DBNull.Value ? null : reader["OrganizationName"].ToString(),
                    Designation = reader["Designation"] == DBNull.Value ? null : reader["Designation"].ToString(),
                    Experience = reader["Experience"] == DBNull.Value ? null : reader["Experience"].ToString()
                });
            }

            return result;
        }

        public async Task<RegisteredEventParticipant?> GetRegisteredParticipantAsync(int eventId, string email)
        {
            using var con = GetConnection();

            const string sql = @"
        SELECT TOP 1
            p.ParticipantId,
            p.FullName,
            p.Email
        FROM dbo.EventRegistrations r
        INNER JOIN dbo.EventRegistrationParticipants p
            ON p.RegistrationId = r.RegistrationId
        WHERE r.EventId = @EventId
          AND LOWER(LTRIM(RTRIM(p.Email))) = @Email
        ORDER BY p.ParticipantId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                .Value = email.Trim().ToLowerInvariant();

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new RegisteredEventParticipant
            {
                ParticipantId = Convert.ToInt32(reader["ParticipantId"]),
                FullName = reader["FullName"].ToString()!,
                Email = reader["Email"].ToString()!
            };
        }


        public async Task<List<object>> GetAllRegistrationsByAdminAsync(Guid adminUserId)
        {
            var result = new List<object>();

            using var con = GetConnection();

            const string sql = @"
        SELECT
            e.EventTitle,

            r.RegistrationId,
            r.EventId,
            r.RegistrationType,
            r.GroupName,
            r.TeamLeadName,
            r.TotalParticipants,
            r.TotalAmount,
            r.RegistrationDate,
            r.RegistrationStatus,

            p.ParticipantId,
            p.FullName,
            p.Gender,
            p.DOB,
            p.Age,
            p.Email,
            p.WhatsAppNo,
            p.Address,
            p.ProfileImage,
            p.RegistrationCategory,
            p.CollegeName,
            p.Stream,
            p.Branch,
            p.AcademicYear,
            p.OrganizationName,
            p.Designation,
            p.Experience

        FROM dbo.AdminEvents e

        INNER JOIN dbo.EventRegistrations r
            ON e.EventId = r.EventId

        INNER JOIN dbo.EventRegistrationParticipants p
            ON r.RegistrationId = p.RegistrationId

        WHERE e.AdminUserId = @AdminUserId

        ORDER BY r.RegistrationDate DESC, p.ParticipantId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier)
                .Value = adminUserId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    EventTitle = reader["EventTitle"],

                    RegistrationId = reader["RegistrationId"],
                    EventId = reader["EventId"],
                    RegistrationType = reader["RegistrationType"],
                    GroupName = reader["GroupName"],
                    TeamLeadName = reader["TeamLeadName"],
                    TotalParticipants = reader["TotalParticipants"],
                    TotalAmount = reader["TotalAmount"],
                    RegistrationDate = reader["RegistrationDate"],
                    RegistrationStatus = reader["RegistrationStatus"],

                    ParticipantId = reader["ParticipantId"],
                    FullName = reader["FullName"],
                    Gender = reader["Gender"],
                    DOB = reader["DOB"],
                    Age = reader["Age"],
                    Email = reader["Email"],
                    WhatsAppNo = reader["WhatsAppNo"],
                    Address = reader["Address"],
                    ProfileImage = reader["ProfileImage"],
                    RegistrationCategory = reader["RegistrationCategory"],
                    CollegeName = reader["CollegeName"],
                    Stream = reader["Stream"],
                    Branch = reader["Branch"],
                    AcademicYear = reader["AcademicYear"],

                    OrganizationName = reader["OrganizationName"] == DBNull.Value
                        ? null : reader["OrganizationName"].ToString(),

                    Designation = reader["Designation"] == DBNull.Value
                        ? null : reader["Designation"].ToString(),

                    Experience = reader["Experience"] == DBNull.Value
                        ? null : reader["Experience"].ToString()
                });
            }

            return result;
        }


        public async Task<object> GetEventRegistrationSummaryAsync(int eventId)
        {
            using var con = GetConnection();

            const string sql = @"
        SELECT
            @EventId AS EventId,
            COUNT(DISTINCT r.RegistrationId) AS TotalRegistrations,
            COUNT(p.ParticipantId) AS TotalParticipants

        FROM dbo.EventRegistrations r

        LEFT JOIN dbo.EventRegistrationParticipants p
            ON r.RegistrationId = p.RegistrationId

        WHERE r.EventId = @EventId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new
                {
                    EventId = Convert.ToInt32(reader["EventId"]),
                    TotalRegistrations = Convert.ToInt32(reader["TotalRegistrations"]),
                    TotalParticipants = Convert.ToInt32(reader["TotalParticipants"])
                };
            }

            return new
            {
                EventId = eventId,
                TotalRegistrations = 0,
                TotalParticipants = 0
            };
        }


        public async Task<(List<RecipientRow> Rows, int Skipped)> GetEventRecipientsForImportAsync(int eventId, CancellationToken ct)
        {
            var rows = new List<RecipientRow>();
            int skipped = 0;

            using var con = GetConnection();

            const string sql = @"
        SELECT
            p.FullName,
            p.Email,
            p.WhatsAppNo,
            r.GroupName
        FROM dbo.EventRegistrations r
        INNER JOIN dbo.EventRegistrationParticipants p
            ON p.RegistrationId = r.RegistrationId
        WHERE r.EventId = @EventId
        ORDER BY r.RegistrationDate DESC, p.ParticipantId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;

            await con.OpenAsync(ct);

            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var fullName = reader["FullName"] == DBNull.Value
                    ? null
                    : reader["FullName"].ToString()?.Trim();

                var email = reader["Email"] == DBNull.Value
                    ? null
                    : reader["Email"].ToString()?.Trim();

                var mobile = reader["WhatsAppNo"] == DBNull.Value
                    ? null
                    : reader["WhatsAppNo"].ToString()?.Trim();

                var groupName = reader["GroupName"] == DBNull.Value
                    ? null
                    : reader["GroupName"].ToString()?.Trim();

                // Skip records that cannot be added as recipients.
                if (string.IsNullOrWhiteSpace(fullName) ||
                    (string.IsNullOrWhiteSpace(email) &&
                     string.IsNullOrWhiteSpace(mobile)))
                {
                    skipped++;
                    continue;
                }

                rows.Add(new RecipientRow
                {
                    FullName = fullName,
                    Email = email,
                    Mobile = mobile,
                    GroupName = groupName,

                    // These fields are not available in event registration.
                    Text1 = null,
                    Text2 = null,
                    Text3 = null,
                    Text4 = null
                });
            }

            return (rows, skipped);
        }
    }
}
