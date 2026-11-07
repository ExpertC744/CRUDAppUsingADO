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
                       DurationUnit, DurationDays, Price, TaxPercentage,
                       Quantity, EventQuantity, DownloadQuantity,
                       Heading1, Content1, Heading2, Content2,
                       IsPopular, DisplayOrder, IsActive
                FROM dbo.SubscriptionPlans
                WHERE (@ActiveOnly = 0 OR IsActive = 1)
                ORDER BY DisplayOrder, PlanId;", con);

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
                    TaxPercentage = reader.GetDecimal(7),
                    Quantity = reader.GetInt32(8),
                    EventQuantity = reader.GetInt32(9),
                    DownloadQuantity = reader.GetInt32(10),
                    Heading1 = reader.IsDBNull(11) ? null : reader.GetString(11),
                    Content1 = reader.IsDBNull(12) ? null : reader.GetString(12),
                    Heading2 = reader.IsDBNull(13) ? null : reader.GetString(13),
                    Content2 = reader.IsDBNull(14) ? null : reader.GetString(14),
                    IsPopular = reader.GetBoolean(15),
                    DisplayOrder = reader.GetInt32(16),
                    IsActive = reader.GetBoolean(17)
                });
            }

            return result;
        }

        public async Task<SubscriptionPlanDto?> GetPlanAsync(int planId)
        {
            var plans = await GetPlansAsync(false);

            return plans.FirstOrDefault(x => x.PlanId == planId);
        }
        //public async Task<SubscriptionPlanDto?> GetPlanAsync(int planId)
        //{
        //    var plans = await GetPlansAsync(false);
        //    return plans.FirstOrDefault(x => x.PlanId == planId);
        //}

        public async Task<PriceCalculationDto> CalculatePriceAsync(
            int planId,
            string? promoCode,
            Guid adminUserId)
        {
            using var con = CreateConnection();
            await con.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT PlanId, Price, TaxPercentage, PlanCategory
                FROM dbo.SubscriptionPlans
                WHERE PlanId = @PlanId AND IsActive = 1;", con);

            cmd.Parameters.AddWithValue("@PlanId", planId);

            decimal price;
            decimal taxPercentage;
            int category;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("Plan not found or inactive.");

                price = reader.GetDecimal(1);
                taxPercentage = reader.GetDecimal(2);
                category = reader.GetByte(3);
            }

            decimal discount = 0;

            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                using var promoCmd = new SqlCommand(@"
                    SELECT TOP 1
                        p.PromoCodeId,
                        p.DiscountType,
                        p.DiscountValue,
                        p.MaximumDiscount,
                        p.MinimumOrderAmount,
                        p.ApplicableCategory,
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
                      );", con);

                promoCmd.Parameters.AddWithValue("@PromoCode", promoCode.Trim());
                promoCmd.Parameters.AddWithValue("@Category", category);
                promoCmd.Parameters.AddWithValue("@PlanId", planId);

                using var reader = await promoCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    throw new InvalidOperationException(
                        "Invalid, expired, or inapplicable promo code.");

                var promoId = reader.GetInt32(0);
                var discountType = reader.GetByte(1);
                var discountValue = reader.GetDecimal(2);
                var maxDiscount = reader.IsDBNull(3)
                    ? (decimal?)null : reader.GetDecimal(3);
                var minAmount = reader.GetDecimal(4);
                var maxUsage = reader.IsDBNull(6)
                    ? (int?)null : reader.GetInt32(6);
                var maxPerAdmin = reader.IsDBNull(7)
                    ? (int?)null : reader.GetInt32(7);
                var currentUsage = reader.GetInt32(8);

                if (price < minAmount)
                    throw new InvalidOperationException(
                        "Plan amount is below the promo minimum order amount.");

                if (maxUsage.HasValue && currentUsage >= maxUsage.Value)
                    throw new InvalidOperationException(
                        "Promo code usage limit has been reached.");

                reader.Close();

                using var usageCmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM dbo.PromoCodeRedemptions
                    WHERE PromoCodeId = @PromoCodeId
                      AND AdminUserId = @AdminUserId
                      AND RedemptionStatus = 'Success';", con);

                usageCmd.Parameters.AddWithValue("@PromoCodeId", promoId);
                usageCmd.Parameters.AddWithValue("@AdminUserId", adminUserId);

                var adminUsage = Convert.ToInt32(
                    await usageCmd.ExecuteScalarAsync());

                if (maxPerAdmin.HasValue &&
                    adminUsage >= maxPerAdmin.Value)
                    throw new InvalidOperationException(
                        "You have already used this promo code the maximum number of times.");

                // DiscountType: 1 = percentage, 2 = fixed amount.
                discount = discountType == 1
                    ? price * discountValue / 100m
                    : discountValue;

                if (maxDiscount.HasValue)
                    discount = Math.Min(discount, maxDiscount.Value);

                discount = Math.Min(discount, price);
            }

            var taxableAmount = price - discount;
            var taxAmount = Math.Round(
                taxableAmount * taxPercentage / 100m, 2,
                MidpointRounding.AwayFromZero);

            return new PriceCalculationDto
            {
                BaseAmount = price,
                DiscountAmount = Math.Round(discount, 2),
                TaxableAmount = Math.Round(taxableAmount, 2),
                TaxPercentage = taxPercentage,
                TaxAmount = taxAmount,
                FinalAmount = Math.Round(
                    taxableAmount + taxAmount, 2,
                    MidpointRounding.AwayFromZero)
            };
        }
    }
}