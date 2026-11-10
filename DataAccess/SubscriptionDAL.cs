using System.Data;
using Microsoft.Data.SqlClient;
using CRUDAppUsingADO.Models;

namespace CRUDAppUsingADO.DataAccess
{
    public class SubscriptionDAL
    {
        private readonly string _connectionString;

        public SubscriptionDAL(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is missing.");
        }

        private SqlConnection CreateConnection()
            => new SqlConnection(_connectionString);

        public async Task<List<SubscriptionPlanDto>> GetPlansAsync(
            bool activeOnly = true)
        {
            var result = new List<SubscriptionPlanDto>();

            using var con = CreateConnection();

            using var cmd = new SqlCommand(@"
                SELECT PlanId, PlanName, PlanCategory, DurationValue,
                       DurationUnit, DurationDays, Price, Currency,
                       Quantity, EventQuantity, DownloadQuantity,
                       Heading1, Content1, Heading2, Content2,
                       IsPopular, DisplayOrder, IsActive
                FROM dbo.SubscriptionPlans
                WHERE (@ActiveOnly = 0 OR IsActive = 1)
                ORDER BY PlanCategory, DisplayOrder, PlanId;", con);

            cmd.Parameters.Add("@ActiveOnly", SqlDbType.Bit)
                .Value = activeOnly;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new SubscriptionPlanDto
                {
                    PlanId = reader.GetInt32(0),
                    PlanName = reader.GetString(1),
                    PlanCategory = reader.GetByte(2),
                    DurationValue = reader.GetInt32(3),
                    DurationUnit = reader.GetString(4),
                    DurationDays = reader.GetInt32(5),
                    Price = reader.GetDecimal(6),
                    Currency = reader.GetString(7).Trim(),

                    // Fixed GST. Do not read a client-supplied value.
                    TaxPercentage = SubscriptionConstants.GstPercentage,

                    Quantity = reader.GetInt32(8),
                    EventQuantity = reader.GetInt32(9),
                    DownloadQuantity = reader.GetInt32(10),
                    Heading1 = reader.IsDBNull(11)
                        ? null : reader.GetString(11),
                    Content1 = reader.IsDBNull(12)
                        ? null : reader.GetString(12),
                    Heading2 = reader.IsDBNull(13)
                        ? null : reader.GetString(13),
                    Content2 = reader.IsDBNull(14)
                        ? null : reader.GetString(14),
                    IsPopular = reader.GetBoolean(15),
                    DisplayOrder = reader.GetInt32(16),
                    IsActive = reader.GetBoolean(17)
                });
            }

            return result;
        }

        public async Task<SubscriptionPlanDto?> GetPlanAsync(int planId)
        {
            if (planId <= 0)
                return null;

            var plans = await GetPlansAsync(false);

            return plans.FirstOrDefault(x => x.PlanId == planId);
        }

        public async Task<PriceCalculationDto> CalculatePriceAsync(
            int planId,
            string? promoCode,
            Guid adminUserId)
        {
            using var con = CreateConnection();
            await con.OpenAsync();

            decimal price;
            int category;

            using (var cmd = new SqlCommand(@"
                SELECT Price, PlanCategory
                FROM dbo.SubscriptionPlans
                WHERE PlanId = @PlanId
                  AND IsActive = 1;", con))
            {
                cmd.Parameters.Add("@PlanId", SqlDbType.Int).Value = planId;

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    throw new InvalidOperationException(
                        "Plan not found or inactive.");

                price = reader.GetDecimal(0);
                category = reader.GetByte(1);
            }

            decimal discount = 0;

            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                int promoId;
                int discountType;
                decimal discountValue;
                decimal? maximumDiscount;
                decimal minimumOrder;
                int? maximumUsage;
                int? maximumPerAdmin;
                int currentUsage;

                using (var promoCmd = new SqlCommand(@"
                    SELECT TOP 1
                        p.PromoCodeId,
                        p.DiscountType,
                        p.DiscountValue,
                        p.MaximumDiscount,
                        p.MinimumOrderAmount,
                        p.MaximumUsage,
                        p.MaximumUsagePerAdmin,
                        p.CurrentUsage
                    FROM dbo.PromoCodes p
                    WHERE UPPER(p.PromoCode) = UPPER(@PromoCode)
                      AND p.IsActive = 1
                      AND p.StartDate <= SYSUTCDATETIME()
                      AND p.ExpiryDate >= SYSUTCDATETIME()
                      AND (p.ApplicableCategory IS NULL
                           OR p.ApplicableCategory = @Category)
                      AND (
                          NOT EXISTS (
                              SELECT 1
                              FROM dbo.PromoCodePlans pp
                              WHERE pp.PromoCodeId = p.PromoCodeId
                          )
                          OR EXISTS (
                              SELECT 1
                              FROM dbo.PromoCodePlans pp
                              WHERE pp.PromoCodeId = p.PromoCodeId
                                AND pp.PlanId = @PlanId
                          )
                      );", con))
                {
                    promoCmd.Parameters.Add("@PromoCode", SqlDbType.NVarChar, 50)
                        .Value = promoCode.Trim();

                    promoCmd.Parameters.Add("@Category", SqlDbType.TinyInt)
                        .Value = category;

                    promoCmd.Parameters.Add("@PlanId", SqlDbType.Int)
                        .Value = planId;

                    using var reader = await promoCmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException(
                            "Invalid, expired, or inapplicable promo code.");

                    promoId = reader.GetInt32(0);
                    discountType = reader.GetByte(1);
                    discountValue = reader.GetDecimal(2);

                    maximumDiscount = reader.IsDBNull(3)
                        ? null : reader.GetDecimal(3);

                    minimumOrder = reader.GetDecimal(4);

                    maximumUsage = reader.IsDBNull(5)
                        ? null : reader.GetInt32(5);

                    maximumPerAdmin = reader.IsDBNull(6)
                        ? null : reader.GetInt32(6);

                    currentUsage = reader.GetInt32(7);
                }

                if (price < minimumOrder)
                    throw new InvalidOperationException(
                        "Plan amount is below the promo minimum order amount.");

                // Count pending, non-expired reservations and successful uses.
                using (var cleanup = new SqlCommand(@"
                    UPDATE pr
                    SET RedemptionStatus = 'Cancelled'
                    FROM dbo.PromoCodeRedemptions pr
                    INNER JOIN dbo.AdminSubscriptions s
                        ON s.SubscriptionId = pr.SubscriptionId
                    WHERE pr.PromoCodeId = @PromoId
                      AND pr.RedemptionStatus = 'Pending'
                      AND s.CreatedAt < DATEADD(MINUTE, -20, SYSUTCDATETIME());",
                    con))
                {
                    cleanup.Parameters.Add("@PromoId", SqlDbType.Int)
                        .Value = promoId;

                    await cleanup.ExecuteNonQueryAsync();
                }

                using (var countCmd = new SqlCommand(@"
                    SELECT
                        COUNT(*),
                        COUNT(CASE WHEN AdminUserId = @AdminId THEN 1 END)
                    FROM dbo.PromoCodeRedemptions
                    WHERE PromoCodeId = @PromoId
                      AND RedemptionStatus IN ('Pending', 'Success');",
                    con))
                {
                    countCmd.Parameters.Add("@PromoId", SqlDbType.Int)
                        .Value = promoId;

                    countCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                        .Value = adminUserId;

                    using var reader = await countCmd.ExecuteReaderAsync();
                    await reader.ReadAsync();

                    int totalUsage = reader.GetInt32(0);
                    int adminUsage = reader.GetInt32(1);

                    if (maximumUsage.HasValue &&
                        totalUsage >= maximumUsage.Value)
                    {
                        throw new InvalidOperationException(
                            "Promo code usage limit has been reached.");
                    }

                    if (maximumPerAdmin.HasValue &&
                        adminUsage >= maximumPerAdmin.Value)
                    {
                        throw new InvalidOperationException(
                            "You have already reached your promo-code usage limit.");
                    }
                }

                // Retain CurrentUsage as a successful-redemption counter.
                if (maximumUsage.HasValue &&
                    currentUsage >= maximumUsage.Value)
                {
                    throw new InvalidOperationException(
                        "Promo code usage limit has been reached.");
                }

                discount = CalculateDiscount(
                    price,
                    discountType,
                    discountValue,
                    maximumDiscount);
            }

            return BuildPriceCalculation(price, discount);
        }

        public async Task<int> CreatePlanAsync(CreatePlanDto dto)
        {
            ValidatePlan(dto);

            using var con = CreateConnection();

            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.SubscriptionPlans
                (
                    PlanName, PlanCategory, DurationValue, DurationUnit,
                    DurationDays, Price, Currency, TaxPercentage,
                    Quantity, EventQuantity, DownloadQuantity,
                    Heading1, Content1, Heading2, Content2,
                    IsPopular, DisplayOrder, IsActive, CreatedAt
                )
                OUTPUT INSERTED.PlanId
                VALUES
                (
                    @PlanName, @PlanCategory, @DurationValue, @DurationUnit,
                    @DurationDays, @Price, @Currency, 18.00,
                    @Quantity, @EventQuantity, @DownloadQuantity,
                    @Heading1, @Content1, @Heading2, @Content2,
                    @IsPopular, @DisplayOrder, 1, SYSUTCDATETIME()
                );", con);

            AddPlanParameters(cmd, dto);

            await con.OpenAsync();

            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<bool> UpdatePlanAsync(
            int planId, CreatePlanDto dto)
        {
            ValidatePlan(dto);

            using var con = CreateConnection();

            using var cmd = new SqlCommand(@"
                UPDATE dbo.SubscriptionPlans
                SET
                    PlanName = @PlanName,
                    PlanCategory = @PlanCategory,
                    DurationValue = @DurationValue,
                    DurationUnit = @DurationUnit,
                    DurationDays = @DurationDays,
                    Price = @Price,
                    Currency = @Currency,
                    TaxPercentage = 18.00,
                    Quantity = @Quantity,
                    EventQuantity = @EventQuantity,
                    DownloadQuantity = @DownloadQuantity,
                    Heading1 = @Heading1,
                    Content1 = @Content1,
                    Heading2 = @Heading2,
                    Content2 = @Content2,
                    IsPopular = @IsPopular,
                    DisplayOrder = @DisplayOrder,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE PlanId = @PlanId;", con);

            cmd.Parameters.Add("@PlanId", SqlDbType.Int).Value = planId;
            AddPlanParameters(cmd, dto);

            await con.OpenAsync();

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> UpdatePlanStatusAsync(
            int planId, bool isActive)
        {
            using var con = CreateConnection();

            using var cmd = new SqlCommand(@"
                UPDATE dbo.SubscriptionPlans
                SET IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE PlanId = @PlanId;", con);

            cmd.Parameters.Add("@PlanId", SqlDbType.Int).Value = planId;
            cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;

            await con.OpenAsync();

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static decimal CalculateDiscount(
            decimal price,
            int discountType,
            decimal discountValue,
            decimal? maximumDiscount)
        {
            decimal discount;

            if (discountType == SubscriptionConstants.PercentageDiscount)
            {
                discount = price * discountValue / 100m;
            }
            else if (discountType == SubscriptionConstants.FixedDiscount)
            {
                discount = discountValue;
            }
            else
            {
                throw new InvalidOperationException(
                    "Invalid promo discount type.");
            }

            if (maximumDiscount.HasValue)
                discount = Math.Min(discount, maximumDiscount.Value);

            return Math.Round(
                Math.Min(discount, price),
                2,
                MidpointRounding.AwayFromZero);
        }

        private static PriceCalculationDto BuildPriceCalculation(
            decimal price, decimal discount)
        {
            decimal taxableAmount = price - discount;

            decimal taxAmount = Math.Round(
                taxableAmount *
                SubscriptionConstants.GstPercentage / 100m,
                2,
                MidpointRounding.AwayFromZero);

            return new PriceCalculationDto
            {
                BaseAmount = price,
                DiscountAmount = discount,
                TaxableAmount = taxableAmount,
                TaxPercentage = SubscriptionConstants.GstPercentage,
                TaxAmount = taxAmount,
                FinalAmount = Math.Round(
                    taxableAmount + taxAmount,
                    2,
                    MidpointRounding.AwayFromZero)
            };
        }

        private static void ValidatePlan(CreatePlanDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PlanName))
                throw new InvalidOperationException("Plan name is required.");

            if (dto.PlanName.Trim().Length > 150)
                throw new InvalidOperationException("Plan name cannot exceed 150 characters.");

            if (dto.PlanCategory is < 1 or > 4)
                throw new InvalidOperationException("Invalid plan category.");

            if (dto.DurationValue <= 0 || dto.DurationDays <= 0)
                throw new InvalidOperationException("Plan duration must be greater than zero.");

            if (string.IsNullOrWhiteSpace(dto.DurationUnit) ||
                dto.DurationUnit.Trim().Length > 20)
                throw new InvalidOperationException("Invalid duration unit.");

            if (dto.Price < 0)
                throw new InvalidOperationException("Price cannot be negative.");

            if (string.IsNullOrWhiteSpace(dto.Currency) ||
                dto.Currency.Trim().Length != 3)
                throw new InvalidOperationException("Currency must be a three-character code.");

            if (dto.Quantity < 0 ||
                dto.EventQuantity < 0 ||
                dto.DownloadQuantity < 0)
                throw new InvalidOperationException("Plan quantities cannot be negative.");

            if (dto.Heading1?.Length > 250 ||
                dto.Heading2?.Length > 250)
                throw new InvalidOperationException("Plan headings cannot exceed 250 characters.");
        }

        private static void AddPlanParameters(
            SqlCommand cmd, CreatePlanDto dto)
        {
            cmd.Parameters.Add("@PlanName", SqlDbType.NVarChar, 150)
                .Value = dto.PlanName.Trim();

            cmd.Parameters.Add("@PlanCategory", SqlDbType.TinyInt)
                .Value = dto.PlanCategory;

            cmd.Parameters.Add("@DurationValue", SqlDbType.Int)
                .Value = dto.DurationValue;

            cmd.Parameters.Add("@DurationUnit", SqlDbType.NVarChar, 20)
                .Value = dto.DurationUnit.Trim();

            cmd.Parameters.Add("@DurationDays", SqlDbType.Int)
                .Value = dto.DurationDays;

            AddDecimal(cmd, "@Price", dto.Price, 18, 2);

            cmd.Parameters.Add("@Currency", SqlDbType.Char, 3)
                .Value = dto.Currency.Trim().ToUpperInvariant();

            cmd.Parameters.Add("@Quantity", SqlDbType.Int)
                .Value = dto.Quantity;

            cmd.Parameters.Add("@EventQuantity", SqlDbType.Int)
                .Value = dto.EventQuantity;

            cmd.Parameters.Add("@DownloadQuantity", SqlDbType.Int)
                .Value = dto.DownloadQuantity;

            cmd.Parameters.Add("@Heading1", SqlDbType.NVarChar, 250)
                .Value = (object?)dto.Heading1 ?? DBNull.Value;

            cmd.Parameters.Add("@Content1", SqlDbType.NVarChar, -1)
                .Value = (object?)dto.Content1 ?? DBNull.Value;

            cmd.Parameters.Add("@Heading2", SqlDbType.NVarChar, 250)
                .Value = (object?)dto.Heading2 ?? DBNull.Value;

            cmd.Parameters.Add("@Content2", SqlDbType.NVarChar, -1)
                .Value = (object?)dto.Content2 ?? DBNull.Value;

            cmd.Parameters.Add("@IsPopular", SqlDbType.Bit)
                .Value = dto.IsPopular;

            cmd.Parameters.Add("@DisplayOrder", SqlDbType.Int)
                .Value = dto.DisplayOrder;
        }

        private static void AddDecimal(
            SqlCommand cmd,
            string name,
            decimal value,
            byte precision,
            byte scale)
        {
            var parameter = cmd.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = precision;
            parameter.Scale = scale;
            parameter.Value = value;
        }
    }
}