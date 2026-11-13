using CRUDAppUsingADO.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess
{
    public class AdminEventDAL
    {
        private readonly IConfiguration _configuration;

        public AdminEventDAL(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        private static object DbValue(object? value)
        {
            return value ?? DBNull.Value;
        }

        private static AdminEventResponseDto Map(SqlDataReader r)
        {
            return new AdminEventResponseDto
            {
                EventId = Convert.ToInt32(r["EventId"]),
                AdminUserId = (Guid)r["AdminUserId"],

                OrganizationName = r["OrganizationName"].ToString()!,
                EventTitle = r["EventTitle"].ToString()!,

                AdvStartDate = Convert.ToDateTime(r["AdvStartDate"]),
                AdvCompletionDate = Convert.ToDateTime(r["AdvCompletionDate"]),
                EventStartDate = Convert.ToDateTime(r["EventStartDate"]),
                EventCompletionDate = Convert.ToDateTime(r["EventCompletionDate"]),

                EventTime = r["EventTime"].ToString()!,
                Venue = r["Venue"].ToString()!,
                City = r["City"].ToString()!,
                State = r["State"].ToString()!,
                Country = r["Country"].ToString()!,

                Image1 = r["Image1"].ToString()!,
                Image2 = r["Image2"] == DBNull.Value ? null : r["Image2"].ToString(),

                AboutEvent = r["AboutEvent"].ToString()!,
                Instructions = r["Instructions"].ToString()!,

                EntryFee = Convert.ToDecimal(r["EntryFee"]),
                PaymentMode = r["PaymentMode"].ToString()!,

                GPay = Convert.ToBoolean(r["GPay"]),
                PhonePe = Convert.ToBoolean(r["PhonePe"]),
                Paytm = Convert.ToBoolean(r["Paytm"]),

                UPIID = r["UPIID"] == DBNull.Value ? null : r["UPIID"].ToString(),
                PaymentMobileNo = r["PaymentMobileNo"] == DBNull.Value
                    ? null : r["PaymentMobileNo"].ToString(),
                QRCodeUrl = r["QRCodeUrl"] == DBNull.Value
                    ? null : r["QRCodeUrl"].ToString(),

                BankName = r["BankName"] == DBNull.Value ? null : r["BankName"].ToString(),
                AccountHolderName = r["AccountHolderName"] == DBNull.Value
                    ? null : r["AccountHolderName"].ToString(),
                AccountNumber = r["AccountNumber"] == DBNull.Value
                    ? null : r["AccountNumber"].ToString(),
                IFSCCode = r["IFSCCode"] == DBNull.Value
                    ? null : r["IFSCCode"].ToString(),
                BranchName = r["BranchName"] == DBNull.Value
                    ? null : r["BranchName"].ToString(),

                nBit = Convert.ToBoolean(r["nBit"]),
                nSABit = Convert.ToBoolean(r["nSABit"]),

                CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
                UpdatedAt = r["UpdatedAt"] == DBNull.Value
                    ? null : Convert.ToDateTime(r["UpdatedAt"]),

                    AttendanceQRCodeUrl =
    r["AttendanceQRCodeUrl"] == DBNull.Value
        ? null
        : r["AttendanceQRCodeUrl"].ToString(),
            };
        }

        private static void AddEventParameters(
            SqlCommand cmd,
            AdminEventSaveDto dto,
            Guid adminUserId,
            string image1,
            string? image2,
            string? qrCode)
        {
            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier)
                .Value = adminUserId;

            cmd.Parameters.Add("@OrganizationName", SqlDbType.NVarChar, 200)
                .Value = dto.OrganizationName.Trim();

            cmd.Parameters.Add("@EventTitle", SqlDbType.NVarChar, 250)
                .Value = dto.EventTitle.Trim();

            cmd.Parameters.Add("@AdvStartDate", SqlDbType.Date)
                .Value = dto.AdvStartDate.Date;

            cmd.Parameters.Add("@AdvCompletionDate", SqlDbType.Date)
                .Value = dto.AdvCompletionDate.Date;

            cmd.Parameters.Add("@EventStartDate", SqlDbType.Date)
                .Value = dto.EventStartDate.Date;

            cmd.Parameters.Add("@EventCompletionDate", SqlDbType.Date)
                .Value = dto.EventCompletionDate.Date;

            cmd.Parameters.Add("@EventTime", SqlDbType.NVarChar, 100)
                .Value = dto.EventTime.Trim();

            cmd.Parameters.Add("@Venue", SqlDbType.NVarChar, 300)
                .Value = dto.Venue.Trim();

            cmd.Parameters.Add("@City", SqlDbType.NVarChar, 100)
                .Value = dto.City.Trim();

            cmd.Parameters.Add("@State", SqlDbType.NVarChar, 100)
                .Value = dto.State.Trim();

            cmd.Parameters.Add("@Country", SqlDbType.NVarChar, 100)
                .Value = dto.Country.Trim();

            cmd.Parameters.Add("@Image1", SqlDbType.NVarChar, 500)
                .Value = image1;

            cmd.Parameters.Add("@Image2", SqlDbType.NVarChar, 500)
                .Value = DbValue(image2);

            cmd.Parameters.Add("@AboutEvent", SqlDbType.NVarChar, 500)
                .Value = dto.AboutEvent.Trim();

            cmd.Parameters.Add("@Instructions", SqlDbType.NVarChar, 500)
                .Value = dto.Instructions.Trim();

            var fee = cmd.Parameters.Add("@EntryFee", SqlDbType.Decimal);
            fee.Precision = 10;
            fee.Scale = 2;
            fee.Value = dto.EntryFee;

            cmd.Parameters.Add("@PaymentMode", SqlDbType.NVarChar, 20)
                .Value = dto.PaymentMode;

            cmd.Parameters.Add("@GPay", SqlDbType.Bit).Value = dto.GPay;
            cmd.Parameters.Add("@PhonePe", SqlDbType.Bit).Value = dto.PhonePe;
            cmd.Parameters.Add("@Paytm", SqlDbType.Bit).Value = dto.Paytm;

            cmd.Parameters.Add("@UPIID", SqlDbType.NVarChar, 150)
                .Value = DbValue(dto.UPIID);

            cmd.Parameters.Add("@PaymentMobileNo", SqlDbType.NVarChar, 20)
                .Value = DbValue(dto.PaymentMobileNo);

            cmd.Parameters.Add("@QRCodeUrl", SqlDbType.NVarChar, 500)
                .Value = DbValue(qrCode);

            cmd.Parameters.Add("@BankName", SqlDbType.NVarChar, 150)
                .Value = DbValue(dto.BankName);

            cmd.Parameters.Add("@AccountHolderName", SqlDbType.NVarChar, 150)
                .Value = DbValue(dto.AccountHolderName);

            cmd.Parameters.Add("@AccountNumber", SqlDbType.NVarChar, 50)
                .Value = DbValue(dto.AccountNumber);

            cmd.Parameters.Add("@IFSCCode", SqlDbType.NVarChar, 20)
                .Value = DbValue(dto.IFSCCode);

            cmd.Parameters.Add("@BranchName", SqlDbType.NVarChar, 150)
                .Value = DbValue(dto.BranchName);
        }

        public async Task<int> CreateAsync(
            Guid adminUserId,
            AdminEventSaveDto dto,
            string image1,
            string? image2,
            string? qrCode)
        {
            using var con = GetConnection();

            const string sql = @"
                INSERT INTO dbo.AdminEvents
                (
                    AdminUserId, OrganizationName, EventTitle,
                    AdvStartDate, AdvCompletionDate,
                    EventStartDate, EventCompletionDate, EventTime,
                    Venue, City, State, Country,
                    Image1, Image2, AboutEvent, Instructions,
                    EntryFee, PaymentMode,
                    GPay, PhonePe, Paytm, UPIID, PaymentMobileNo,
                    QRCodeUrl, BankName, AccountHolderName,
                    AccountNumber, IFSCCode, BranchName,
                    nBit, nSABit
                )
                VALUES
                (
                    @AdminUserId, @OrganizationName, @EventTitle,
                    @AdvStartDate, @AdvCompletionDate,
                    @EventStartDate, @EventCompletionDate, @EventTime,
                    @Venue, @City, @State, @Country,
                    @Image1, @Image2, @AboutEvent, @Instructions,
                    @EntryFee, @PaymentMode,
                    @GPay, @PhonePe, @Paytm, @UPIID, @PaymentMobileNo,
                    @QRCodeUrl, @BankName, @AccountHolderName,
                    @AccountNumber, @IFSCCode, @BranchName,
                    1, 1
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var cmd = new SqlCommand(sql, con);

            AddEventParameters(
                cmd, dto, adminUserId, image1, image2, qrCode);

            await con.OpenAsync();

            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<AdminEventResponseDto?> GetByIdAsync(int eventId, Guid adminUserId)
        {
            using var con = GetConnection();
            const string sql = @"SELECT * FROM dbo.AdminEvents WHERE EventId = @EventId AND AdminUserId = @AdminUserId;";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier).Value = adminUserId;
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? Map(reader) : null;
        }

        public async Task<List<AdminEventResponseDto>> GetAllByAdminAsync(Guid adminUserId)
        {
            var list = new List<AdminEventResponseDto>();
            using var con = GetConnection();
            const string sql = @"SELECT * FROM dbo.AdminEvents WHERE AdminUserId = @AdminUserId ORDER BY CreatedAt DESC;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier).Value = adminUserId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(Map(reader));

            return list;
        }

        public async Task<bool> UpdateAsync(int eventId, Guid adminUserId, AdminEventSaveDto dto, string image1, string? image2, string? qrCode)
        {
            using var con = GetConnection();

            const string sql = @"
                UPDATE dbo.AdminEvents
                SET
                    OrganizationName = @OrganizationName,
                    EventTitle = @EventTitle,
                    AdvStartDate = @AdvStartDate,
                    AdvCompletionDate = @AdvCompletionDate,
                    EventStartDate = @EventStartDate,
                    EventCompletionDate = @EventCompletionDate,
                    EventTime = @EventTime,
                    Venue = @Venue,
                    City = @City,
                    State = @State,
                    Country = @Country,
                    Image1 = @Image1,
                    Image2 = @Image2,
                    AboutEvent = @AboutEvent,
                    Instructions = @Instructions,
                    EntryFee = @EntryFee,
                    PaymentMode = @PaymentMode,
                    GPay = @GPay,
                    PhonePe = @PhonePe,
                    Paytm = @Paytm,
                    UPIID = @UPIID,
                    PaymentMobileNo = @PaymentMobileNo,
                    QRCodeUrl = @QRCodeUrl,
                    BankName = @BankName,
                    AccountHolderName = @AccountHolderName,
                    AccountNumber = @AccountNumber,
                    IFSCCode = @IFSCCode,
                    BranchName = @BranchName,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE EventId = @EventId
                  AND AdminUserId = @AdminUserId;

                SELECT @@ROWCOUNT;";

            using var cmd = new SqlCommand(sql, con);
            AddEventParameters(cmd, dto, adminUserId, image1, image2, qrCode);
            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
            await con.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<bool> UpdateAdminStatusAsync(int eventId, Guid adminUserId, bool enabled)
        {
            using var con = GetConnection();

            const string sql = @"
                UPDATE dbo.AdminEvents
                SET nBit = @nBit,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE EventId = @EventId
                  AND AdminUserId = @AdminUserId;

                SELECT @@ROWCOUNT;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
            cmd.Parameters.Add("@AdminUserId", SqlDbType.UniqueIdentifier)
                .Value = adminUserId;
            cmd.Parameters.Add("@nBit", SqlDbType.Bit).Value = enabled;

            await con.OpenAsync();

            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<bool> UpdateSuperAdminStatusAsync(int eventId, bool enabled)
        {
            using var con = GetConnection();
            const string sql = @"
        UPDATE dbo.AdminEvents
        SET nSABit = @nSABit,
            UpdatedAt = SYSUTCDATETIME()
        WHERE EventId = @EventId;

        SELECT @@ROWCOUNT;";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
            cmd.Parameters.Add("@nSABit", SqlDbType.Bit).Value = enabled;
            await con.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }


        public async Task<List<AdminEventResponseDto>> GetPublicEventsAsync()
        {
            var list = new List<AdminEventResponseDto>();

            using var con = GetConnection();

            const string sql = @"
        SELECT *
        FROM dbo.AdminEvents
        WHERE nBit = 1
          AND nSABit = 1
          AND AdvStartDate <= CAST(GETDATE() AS DATE)
          AND AdvCompletionDate >= CAST(GETDATE() AS DATE)
          AND EventCompletionDate >= CAST(GETDATE() AS DATE)
          AND EventCompletionDate >= EventStartDate
        ORDER BY EventStartDate ASC;";

            using var cmd = new SqlCommand(sql, con);

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(Map(reader));

            return list;
        }

        public async Task<AdminEventResponseDto?> GetPublicEventByIdAsync(int eventId)
        {
            using var con = GetConnection();

            const string sql = @"
        SELECT *
        FROM dbo.AdminEvents
        WHERE EventId = @EventId
          AND nBit = 1
          AND nSABit = 1
          AND AdvStartDate <= CAST(GETDATE() AS DATE)
          AND AdvCompletionDate >= CAST(GETDATE() AS DATE)
          AND EventCompletionDate >= CAST(GETDATE() AS DATE)
          AND EventCompletionDate >= EventStartDate;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int).Value = eventId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? Map(reader) : null;
        }


        public async Task UpdateAttendanceQRCodeUrlAsync(int eventId, string qrCodeUrl)
        {
            using var con = GetConnection();

            const string sql = @"
        UPDATE dbo.AdminEvents
        SET AttendanceQRCodeUrl = @QRCodeUrl
        WHERE EventId = @EventId;";

            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add("@EventId", SqlDbType.Int)
                .Value = eventId;

            cmd.Parameters.Add("@QRCodeUrl", SqlDbType.NVarChar, 500)
                .Value = qrCodeUrl;

            await con.OpenAsync();

            await cmd.ExecuteNonQueryAsync();
        }

    }
}