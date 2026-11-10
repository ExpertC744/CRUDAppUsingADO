using System.Data;
using Microsoft.Data.SqlClient;
using CRUDAppUsingADO.Models;

namespace CRUDAppUsingADO.DataAccess
{
    public class PromoCodeDAL
    {
        private readonly string _connectionString;

        public PromoCodeDAL(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is missing.");
        }

        private SqlConnection CreateConnection()
            => new SqlConnection(_connectionString);

        // GET ALL PROMO CODES
        public async Task<List<PromoCodeDto>> GetAllAsync()
        {
            var result = new List<PromoCodeDto>();

            using var con = CreateConnection();
            using var cmd = new SqlCommand(@"
                SELECT PromoCodeId, PromoCode, PromoName,
                       VisibilityType, DiscountType, DiscountValue,
                       MaximumDiscount, MinimumOrderAmount,
                       ApplicableCategory, StartDate, ExpiryDate,
                       MaximumUsage, MaximumUsagePerAdmin,
                       CurrentUsage, IsActive
                FROM dbo.PromoCodes
                ORDER BY CreatedAt DESC;", con);

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new PromoCodeDto
                {
                    PromoCodeId = reader.GetInt32(0),
                    PromoCode = reader.GetString(1),
                    PromoName = reader.GetString(2),
                    VisibilityType = reader.GetByte(3),
                    DiscountType = reader.GetByte(4),
                    DiscountValue = reader.GetDecimal(5),
                    MaximumDiscount = reader.IsDBNull(6)
                        ? null : reader.GetDecimal(6),
                    MinimumOrderAmount = reader.GetDecimal(7),
                    ApplicableCategory = reader.IsDBNull(8)
                        ? null : reader.GetByte(8),
                    StartDate = reader.GetDateTime(9),
                    ExpiryDate = reader.GetDateTime(10),
                    MaximumUsage = reader.IsDBNull(11)
                        ? null : reader.GetInt32(11),
                    MaximumUsagePerAdmin = reader.IsDBNull(12)
                        ? null : reader.GetInt32(12),
                    CurrentUsage = reader.GetInt32(13),
                    IsActive = reader.GetBoolean(14)
                });
            }

            await reader.CloseAsync();

            foreach (var promo in result)
            {
                promo.PlanIds = await GetPlanIdsAsync(
                    con, promo.PromoCodeId);
            }

            return result;
        }

        // GET PROMO CODE BY ID
        public async Task<PromoCodeDto?> GetByIdAsync(int id)
        {
            var promos = await GetAllAsync();

            return promos.FirstOrDefault(x => x.PromoCodeId == id);
        }

        // CREATE PROMO CODE
        public async Task<int> CreateAsync(CreatePromoCodeDto dto)
        {
            ValidatePromo(dto);

            using var con = CreateConnection();
            await con.OpenAsync();

            using var transaction = con.BeginTransaction();

            try
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.PromoCodes
                    (
                        PromoCode, PromoName, VisibilityType,
                        DiscountType, DiscountValue, MaximumDiscount,
                        MinimumOrderAmount, ApplicableCategory,
                        StartDate, ExpiryDate, MaximumUsage,
                        MaximumUsagePerAdmin, CurrentUsage,
                        IsActive, CreatedAt
                    )
                    VALUES
                    (
                        @PromoCode, @PromoName, @VisibilityType,
                        @DiscountType, @DiscountValue, @MaximumDiscount,
                        @MinimumOrderAmount, @ApplicableCategory,
                        @StartDate, @ExpiryDate, @MaximumUsage,
                        @MaximumUsagePerAdmin, 0,
                        1, SYSUTCDATETIME()
                    );

                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    con, transaction);

                AddPromoParameters(cmd, dto);

                int promoId = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync());

                await SavePlanMappingsAsync(
                    con, transaction, promoId, dto.PlanIds);

                transaction.Commit();

                return promoId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // UPDATE PROMO CODE
        public async Task<bool> UpdateAsync(
            int id, CreatePromoCodeDto dto)
        {
            ValidatePromo(dto);

            using var con = CreateConnection();
            await con.OpenAsync();

            using var transaction = con.BeginTransaction();

            try
            {
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.PromoCodes
                    SET
                        PromoCode = @PromoCode,
                        PromoName = @PromoName,
                        VisibilityType = @VisibilityType,
                        DiscountType = @DiscountType,
                        DiscountValue = @DiscountValue,
                        MaximumDiscount = @MaximumDiscount,
                        MinimumOrderAmount = @MinimumOrderAmount,
                        ApplicableCategory = @ApplicableCategory,
                        StartDate = @StartDate,
                        ExpiryDate = @ExpiryDate,
                        MaximumUsage = @MaximumUsage,
                        MaximumUsagePerAdmin = @MaximumUsagePerAdmin,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE PromoCodeId = @PromoCodeId;",
                    con, transaction);

                cmd.Parameters.Add("@PromoCodeId", SqlDbType.Int).Value = id;
                AddPromoParameters(cmd, dto);

                int affected = await cmd.ExecuteNonQueryAsync();

                if (affected == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                using (var deleteCmd = new SqlCommand(@"
                    DELETE FROM dbo.PromoCodePlans
                    WHERE PromoCodeId = @PromoCodeId;",
                    con, transaction))
                {
                    deleteCmd.Parameters.Add("@PromoCodeId", SqlDbType.Int)
                        .Value = id;

                    await deleteCmd.ExecuteNonQueryAsync();
                }

                await SavePlanMappingsAsync(
                    con, transaction, id, dto.PlanIds);

                transaction.Commit();

                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ENABLE / DISABLE PROMO CODE
        public async Task<bool> UpdateStatusAsync(
            int id, bool isActive)
        {
            using var con = CreateConnection();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.PromoCodes
                SET IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE PromoCodeId = @PromoCodeId;", con);

            cmd.Parameters.Add("@PromoCodeId", SqlDbType.Int).Value = id;
            cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;

            await con.OpenAsync();

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static async Task<List<int>> GetPlanIdsAsync(
            SqlConnection con, int promoId)
        {
            var ids = new List<int>();

            using var cmd = new SqlCommand(@"
                SELECT PlanId
                FROM dbo.PromoCodePlans
                WHERE PromoCodeId = @PromoCodeId
                ORDER BY PlanId;", con);

            cmd.Parameters.Add("@PromoCodeId", SqlDbType.Int).Value = promoId;

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                ids.Add(reader.GetInt32(0));

            return ids;
        }

        private static async Task SavePlanMappingsAsync(
            SqlConnection con,
            SqlTransaction transaction,
            int promoId,
            List<int>? planIds)
        {
            if (planIds == null || planIds.Count == 0)
                return;

            var uniqueIds = planIds.Distinct().ToList();

            foreach (int planId in uniqueIds)
            {
                using var checkCmd = new SqlCommand(@"
                    IF NOT EXISTS
                    (
                        SELECT 1 FROM dbo.SubscriptionPlans
                        WHERE PlanId = @PlanId
                    )
                        THROW 50001, 'Invalid Plan ID in promo code mapping.', 1;",
                    con, transaction);

                checkCmd.Parameters.Add("@PlanId", SqlDbType.Int).Value =
                    planId;

                await checkCmd.ExecuteNonQueryAsync();

                using var insertCmd = new SqlCommand(@"
                    INSERT INTO dbo.PromoCodePlans
                    (PromoCodeId, PlanId)
                    VALUES (@PromoCodeId, @PlanId);",
                    con, transaction);

                insertCmd.Parameters.Add("@PromoCodeId", SqlDbType.Int)
                    .Value = promoId;

                insertCmd.Parameters.Add("@PlanId", SqlDbType.Int)
                    .Value = planId;

                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        private static void AddPromoParameters(
            SqlCommand cmd, CreatePromoCodeDto dto)
        {
            cmd.Parameters.Add("@PromoCode", SqlDbType.NVarChar, 50)
                .Value = dto.PromoCode.Trim().ToUpperInvariant();

            cmd.Parameters.Add("@PromoName", SqlDbType.NVarChar, 150)
                .Value = dto.PromoName.Trim();

            cmd.Parameters.Add("@VisibilityType", SqlDbType.TinyInt)
                .Value = dto.VisibilityType;

            cmd.Parameters.Add("@DiscountType", SqlDbType.TinyInt)
                .Value = dto.DiscountType;

            AddDecimal(cmd, "@DiscountValue", dto.DiscountValue, 18, 2);
            AddNullableDecimal(cmd, "@MaximumDiscount", dto.MaximumDiscount);
            AddDecimal(cmd, "@MinimumOrderAmount", dto.MinimumOrderAmount, 18, 2);

            cmd.Parameters.Add("@ApplicableCategory", SqlDbType.TinyInt)
                .Value = (object?)dto.ApplicableCategory ?? DBNull.Value;

            cmd.Parameters.Add("@StartDate", SqlDbType.DateTime2)
                .Value = dto.StartDate;

            cmd.Parameters.Add("@ExpiryDate", SqlDbType.DateTime2)
                .Value = dto.ExpiryDate;

            cmd.Parameters.Add("@MaximumUsage", SqlDbType.Int)
                .Value = (object?)dto.MaximumUsage ?? DBNull.Value;

            cmd.Parameters.Add("@MaximumUsagePerAdmin", SqlDbType.Int)
                .Value = (object?)dto.MaximumUsagePerAdmin ?? DBNull.Value;
        }

        private static void AddDecimal(
            SqlCommand cmd, string name, decimal value,
            byte precision, byte scale)
        {
            var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
            p.Precision = precision;
            p.Scale = scale;
            p.Value = value;
        }

        private static void AddNullableDecimal(
            SqlCommand cmd, string name, decimal? value)
        {
            var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
            p.Precision = 18;
            p.Scale = 2;
            p.Value = (object?)value ?? DBNull.Value;
        }

        private static void ValidatePromo(CreatePromoCodeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PromoCode))
                throw new InvalidOperationException("Promo code is required.");

            dto.PromoCode = dto.PromoCode.Trim().ToUpperInvariant();

            if (dto.PromoCode.Length > 50)
                throw new InvalidOperationException(
                    "Promo code cannot exceed 50 characters.");

            if (string.IsNullOrWhiteSpace(dto.PromoName))
                throw new InvalidOperationException("Promo name is required.");

            if (dto.PromoName.Trim().Length > 150)
                throw new InvalidOperationException(
                    "Promo name cannot exceed 150 characters.");

            if (dto.VisibilityType is not (
                SubscriptionConstants.PrivatePromo or
                SubscriptionConstants.PublicPromo))
            {
                throw new InvalidOperationException(
                    "VisibilityType must be 1 (private) or 2 (public).");
            }

            if (dto.DiscountType is not (
                SubscriptionConstants.PercentageDiscount or
                SubscriptionConstants.FixedDiscount))
            {
                throw new InvalidOperationException(
                    "DiscountType must be 1 (percentage) or 2 (fixed amount).");
            }

            if (dto.DiscountValue <= 0)
                throw new InvalidOperationException(
                    "Discount value must be greater than zero.");

            if (dto.DiscountType == SubscriptionConstants.PercentageDiscount &&
                dto.DiscountValue > 100)
            {
                throw new InvalidOperationException(
                    "Percentage discount cannot exceed 100%.");
            }

            if (dto.MaximumDiscount.HasValue &&
                dto.MaximumDiscount.Value < 0)
            {
                throw new InvalidOperationException(
                    "Maximum discount cannot be negative.");
            }

            if (dto.MinimumOrderAmount < 0)
                throw new InvalidOperationException(
                    "Minimum order amount cannot be negative.");

            if (dto.MaximumUsage.HasValue && dto.MaximumUsage.Value <= 0)
                throw new InvalidOperationException(
                    "Maximum usage must be greater than zero.");

            if (dto.MaximumUsagePerAdmin.HasValue &&
                dto.MaximumUsagePerAdmin.Value <= 0)
            {
                throw new InvalidOperationException(
                    "Maximum usage per admin must be greater than zero.");
            }

            if (dto.StartDate >= dto.ExpiryDate)
                throw new InvalidOperationException(
                    "Expiry date must be later than start date.");

            if (dto.ApplicableCategory.HasValue &&
                dto.ApplicableCategory.Value is < 1 or > 4)
            {
                throw new InvalidOperationException(
                    "Invalid applicable plan category.");
            }

            if (dto.PlanIds != null && dto.PlanIds.Any(x => x <= 0))
                throw new InvalidOperationException("Invalid plan ID.");

            if (dto.PlanIds != null &&
                dto.PlanIds.Distinct().Count() != dto.PlanIds.Count)
            {
                throw new InvalidOperationException(
                    "Duplicate plan IDs are not allowed.");
            }
        }

        public async Task<List<PromoCodeDto>> GetPublicActiveAsync()
        {
            var result = new List<PromoCodeDto>();

            using var con = CreateConnection();
            await con.OpenAsync();

            using var cmd = new SqlCommand(@"
        SELECT PromoCodeId, PromoCode, PromoName,
               VisibilityType, DiscountType, DiscountValue,
               MaximumDiscount, MinimumOrderAmount,
               ApplicableCategory, StartDate, ExpiryDate,
               MaximumUsage, MaximumUsagePerAdmin,
               CurrentUsage, IsActive
        FROM dbo.PromoCodes
        WHERE IsActive = 1
          AND VisibilityType = 2
          AND StartDate <= SYSUTCDATETIME()
          AND ExpiryDate >= SYSUTCDATETIME()
          AND (MaximumUsage IS NULL OR CurrentUsage < MaximumUsage)
        ORDER BY CreatedAt DESC;", con);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new PromoCodeDto
                {
                    PromoCodeId = reader.GetInt32(0),
                    PromoCode = reader.GetString(1),
                    PromoName = reader.GetString(2),
                    VisibilityType = reader.GetByte(3),
                    DiscountType = reader.GetByte(4),
                    DiscountValue = reader.GetDecimal(5),
                    MaximumDiscount = reader.IsDBNull(6)
                        ? null : reader.GetDecimal(6),
                    MinimumOrderAmount = reader.GetDecimal(7),
                    ApplicableCategory = reader.IsDBNull(8)
                        ? null : reader.GetByte(8),
                    StartDate = reader.GetDateTime(9),
                    ExpiryDate = reader.GetDateTime(10),
                    MaximumUsage = reader.IsDBNull(11)
                        ? null : reader.GetInt32(11),
                    MaximumUsagePerAdmin = reader.IsDBNull(12)
                        ? null : reader.GetInt32(12),
                    CurrentUsage = reader.GetInt32(13),
                    IsActive = reader.GetBoolean(14)
                });
            }

            await reader.CloseAsync();

            foreach (var promo in result)
            {
                promo.PlanIds = await GetPlanIdsAsync(con, promo.PromoCodeId);
            }

            return result;
        }
    }
}