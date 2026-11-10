using System.Data;
using CRUDAppUsingADO.Models;
using Microsoft.Data.SqlClient;
using CRUDAppUsingADO.Services;

namespace CRUDAppUsingADO.DataAccess
{
    public class SubscriptionPaymentDAL
    {
        private readonly string _connectionString;

        public SubscriptionPaymentDAL(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is missing.");
        }

        private SqlConnection Connection() => new SqlConnection(_connectionString);

        public async Task<CheckoutResponseDto> CreateCheckoutAsync(Guid adminId, int planId, string? promoCode, IRazorpayService razorpay)
        {
            decimal baseAmount, discount, taxAmount, finalAmount;
            decimal taxPercentage;
            string planName, currency;
            long subscriptionId, paymentId;
            int? promoId = null;
            int category, durationDays;

            // First create a pending subscription and payment record.
            using (var con = Connection())
            {
                await con.OpenAsync();

                using var tx = con.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    using var planCmd = new SqlCommand(@"
    SELECT PlanName, PlanCategory, DurationDays,
           Price, Currency
    FROM dbo.SubscriptionPlans WITH (UPDLOCK, HOLDLOCK)
    WHERE PlanId = @PlanId AND IsActive = 1;",
    con, tx);

                    planCmd.Parameters.Add("@PlanId", SqlDbType.Int)
                        .Value = planId;

                    using var planReader = await planCmd.ExecuteReaderAsync();

                    if (!await planReader.ReadAsync())
                        throw new InvalidOperationException(
                            "Selected plan is unavailable.");

                    planName = planReader.GetString(0);
                    category = planReader.GetByte(1);
                    durationDays = planReader.GetInt32(2);
                    baseAmount = planReader.GetDecimal(3);
                    currency = planReader.GetString(4).Trim();

                    taxPercentage = SubscriptionConstants.GstPercentage;

                    await planReader.CloseAsync();

                    discount = 0;

                    if (!string.IsNullOrWhiteSpace(promoCode))
                    {
                        using var promoCmd = new SqlCommand(@"
                            SELECT PromoCodeId, DiscountType,
                                   DiscountValue, MaximumDiscount,
                                   MinimumOrderAmount, MaximumUsage,
                                   MaximumUsagePerAdmin, CurrentUsage
                            FROM dbo.PromoCodes WITH (UPDLOCK, HOLDLOCK)
                            WHERE UPPER(PromoCode) = UPPER(@Code)
                              AND IsActive = 1
                              AND StartDate <= SYSUTCDATETIME()
                              AND ExpiryDate >= SYSUTCDATETIME()
                              AND (ApplicableCategory IS NULL
                                   OR ApplicableCategory = @Category)
                              AND (
                                  NOT EXISTS (
                                      SELECT 1
                                      FROM dbo.PromoCodePlans pp
                                      WHERE pp.PromoCodeId = PromoCodes.PromoCodeId
                                  )
                                  OR EXISTS (
                                      SELECT 1
                                      FROM dbo.PromoCodePlans pp
                                      WHERE pp.PromoCodeId = PromoCodes.PromoCodeId
                                        AND pp.PlanId = @PlanId
                                  )
                              );", con, tx);

                        promoCmd.Parameters.Add("@Code", SqlDbType.NVarChar, 100)
                            .Value = promoCode.Trim().ToUpperInvariant();

                        promoCmd.Parameters.Add("@Category", SqlDbType.TinyInt)
                            .Value = category;

                        promoCmd.Parameters.Add("@PlanId", SqlDbType.Int)
                            .Value = planId;

                        using var promoReader = await promoCmd.ExecuteReaderAsync();

                        if (!await promoReader.ReadAsync())
                            throw new InvalidOperationException(
                                "Promo code is invalid, expired, or not applicable.");

                        promoId = promoReader.GetInt32(0);
                        var discountType = promoReader.GetByte(1);
                        var discountValue = promoReader.GetDecimal(2);
                        var maximumDiscount = promoReader.IsDBNull(3)
                            ? (decimal?)null : promoReader.GetDecimal(3);
                        var minimumOrder = promoReader.GetDecimal(4);
                        var maximumUsage = promoReader.IsDBNull(5)
                            ? (int?)null : promoReader.GetInt32(5);
                        var maximumPerAdmin = promoReader.IsDBNull(6)
                            ? (int?)null : promoReader.GetInt32(6);
                        var currentUsage = promoReader.GetInt32(7);

                        await promoReader.CloseAsync();

                        if (baseAmount < minimumOrder)
                            throw new InvalidOperationException(
                                "Plan price does not meet the promo minimum.");

                        using (var cleanupCmd = new SqlCommand(@"
    UPDATE pr
    SET RedemptionStatus = 'Cancelled'
    FROM dbo.PromoCodeRedemptions pr
    INNER JOIN dbo.AdminSubscriptions s
        ON s.SubscriptionId = pr.SubscriptionId
    WHERE pr.PromoCodeId = @PromoId
      AND pr.RedemptionStatus = 'Pending'
      AND s.CreatedAt < DATEADD(MINUTE, -20, SYSUTCDATETIME());",
    con, tx))
                        {
                            cleanupCmd.Parameters.Add("@PromoId", SqlDbType.Int)
                                .Value = promoId.Value;

                            await cleanupCmd.ExecuteNonQueryAsync();
                        }


                        // Count pending reservations as well as successful redemptions.
                        using var countCmd = new SqlCommand(@"
                            SELECT
                                COUNT(CASE WHEN RedemptionStatus IN
                                    ('Pending', 'Success') THEN 1 END),
                                COUNT(CASE WHEN AdminUserId = @AdminId
                                    AND RedemptionStatus IN
                                    ('Pending', 'Success') THEN 1 END)
                            FROM dbo.PromoCodeRedemptions WITH (UPDLOCK, HOLDLOCK)
                            WHERE PromoCodeId = @PromoId;", con, tx);

                        countCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                            .Value = adminId;

                        countCmd.Parameters.Add("@PromoId", SqlDbType.Int)
                            .Value = promoId.Value;

                        using var countReader = await countCmd.ExecuteReaderAsync();

                        await countReader.ReadAsync();

                        int totalReserved = countReader.GetInt32(0);
                        int adminReserved = countReader.GetInt32(1);

                        await countReader.CloseAsync();

                        if (maximumUsage.HasValue &&
                            totalReserved >= maximumUsage.Value)
                            throw new InvalidOperationException(
                                "Promo code usage limit has been reached.");

                        if (maximumPerAdmin.HasValue &&
                            adminReserved >= maximumPerAdmin.Value)
                            throw new InvalidOperationException(
                                "You have reached the usage limit for this promo code.");

                        // Keep CurrentUsage as successful redemptions only.
                        //if (maximumUsage.HasValue &&
                        //    currentUsage >= maximumUsage.Value)
                        //    throw new InvalidOperationException(
                        //        "Promo code usage limit has been reached.");

                        discount = discountType == 1
                            ? baseAmount * discountValue / 100m
                            : discountValue;

                        if (maximumDiscount.HasValue)
                            discount = Math.Min(discount, maximumDiscount.Value);

                        discount = Math.Min(discount, baseAmount);
                    }

                    discount = Math.Round(discount, 2);
                    var taxableAmount = baseAmount - discount;

                    taxAmount = Math.Round(
                        taxableAmount * taxPercentage / 100m,
                        2, MidpointRounding.AwayFromZero);

                    finalAmount = Math.Round(
                        taxableAmount + taxAmount,
                        2, MidpointRounding.AwayFromZero);

                    using var subCmd = new SqlCommand(@"
                        INSERT INTO dbo.AdminSubscriptions
                        (
                            AdminUserId, PlanId, PromoCodeId,
                            PlanName, PlanCategory, DurationDays,
                            BaseAmount, DiscountAmount, TaxAmount,
                            FinalAmount, Currency, StartDate, EndDate,
                            SubscriptionStatus, CreatedAt
                        )
                        OUTPUT INSERTED.SubscriptionId
                        VALUES
                        (
                            @AdminId, @PlanId, @PromoId,
                            @PlanName, @Category, @DurationDays,
                            @Base, @Discount, @Tax, @Final, @Currency,
                            NULL, NULL, 'Pending', SYSUTCDATETIME()
                        );", con, tx);

                    subCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                        .Value = adminId;
                    subCmd.Parameters.Add("@PlanId", SqlDbType.Int)
                        .Value = planId;
                    subCmd.Parameters.Add("@PromoId", SqlDbType.Int)
                        .Value = (object?)promoId ?? DBNull.Value;
                    subCmd.Parameters.Add("@PlanName", SqlDbType.NVarChar, 300)
                        .Value = planName;
                    subCmd.Parameters.Add("@Category", SqlDbType.TinyInt)
                        .Value = category;
                    subCmd.Parameters.Add("@DurationDays", SqlDbType.Int)
                        .Value = durationDays;

                    AddMoney(subCmd, "@Base", baseAmount);
                    AddMoney(subCmd, "@Discount", discount);
                    AddMoney(subCmd, "@Tax", taxAmount);
                    AddMoney(subCmd, "@Final", finalAmount);

                    subCmd.Parameters.Add("@Currency", SqlDbType.Char, 3)
                        .Value = currency;

                    subscriptionId = Convert.ToInt64(
                        await subCmd.ExecuteScalarAsync());

                    using var paymentCmd = new SqlCommand(@"
                        INSERT INTO dbo.SubscriptionPayments
                        (
                            SubscriptionId, AdminUserId, GatewayName,
                            Amount, Currency, PaymentStatus, CreatedAt
                        )
                        OUTPUT INSERTED.PaymentId
                        VALUES
                        (
                            @SubscriptionId, @AdminId, 'Razorpay',
                            @Amount, @Currency, 'Created', SYSUTCDATETIME()
                        );", con, tx);

                    paymentCmd.Parameters.Add("@SubscriptionId", SqlDbType.BigInt)
                        .Value = subscriptionId;
                    paymentCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                        .Value = adminId;
                    AddMoney(paymentCmd, "@Amount", finalAmount);
                    paymentCmd.Parameters.Add("@Currency", SqlDbType.Char, 3)
                        .Value = currency;

                    paymentId = Convert.ToInt64(
                        await paymentCmd.ExecuteScalarAsync());

                    if (promoId.HasValue)
                    {
                        using var redemptionCmd = new SqlCommand(@"
                            INSERT INTO dbo.PromoCodeRedemptions
                            (
                                PromoCodeId, SubscriptionId, AdminUserId,
                                DiscountAmount, RedemptionStatus, CreatedAt
                            )
                            VALUES
                            (
                                @PromoId, @SubscriptionId, @AdminId,
                                @Discount, 'Pending', SYSUTCDATETIME()
                            );", con, tx);

                        redemptionCmd.Parameters.Add("@PromoId", SqlDbType.Int)
                            .Value = promoId.Value;
                        redemptionCmd.Parameters.Add("@SubscriptionId", SqlDbType.BigInt)
                            .Value = subscriptionId;
                        redemptionCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                            .Value = adminId;
                        AddMoney(redemptionCmd, "@Discount", discount);

                        await redemptionCmd.ExecuteNonQueryAsync();
                    }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            // Create the Razorpay order after saving the local records.
            try
            {
                var orderId = await razorpay.CreateOrderAsync(
                    finalAmount,
                    $"SUB-{subscriptionId}");

                using var con = Connection();
                await con.OpenAsync();

                using var update = new SqlCommand(@"
                    UPDATE dbo.SubscriptionPayments
                    SET GatewayOrderId = @OrderId,
                        PaymentStatus = 'Pending',
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE PaymentId = @PaymentId;", con);

                update.Parameters.Add("@OrderId", SqlDbType.NVarChar, 300)
                    .Value = orderId;

                update.Parameters.Add("@PaymentId", SqlDbType.BigInt)
                    .Value = paymentId;

                await update.ExecuteNonQueryAsync();

                return new CheckoutResponseDto
                {
                    PaymentId = paymentId,
                    SubscriptionId = subscriptionId,
                    RazorpayOrderId = orderId,
                    BaseAmount = baseAmount,
                    DiscountAmount = discount,
                    TaxAmount = taxAmount,
                    FinalAmount = finalAmount,
                    Currency = currency,
                    PlanName = planName
                };
            }
            catch
            {
                await MarkCheckoutFailedAsync(paymentId, subscriptionId);
                throw;
            }
        }

        private async Task MarkCheckoutFailedAsync(
            long paymentId, long subscriptionId)
        {
            using var con = Connection();
            await con.OpenAsync();

            using var tx = con.BeginTransaction();

            try
            {
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.SubscriptionPayments
                    SET PaymentStatus = 'Failed',
                        FailureReason = 'Razorpay order creation failed.',
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE PaymentId = @PaymentId;

                    UPDATE dbo.AdminSubscriptions
                    SET SubscriptionStatus = 'Failed',
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE SubscriptionId = @SubscriptionId;

                    UPDATE dbo.PromoCodeRedemptions
                    SET RedemptionStatus = 'Cancelled'
                    WHERE SubscriptionId = @SubscriptionId
                      AND RedemptionStatus = 'Pending';", con, tx);

                cmd.Parameters.Add("@PaymentId", SqlDbType.BigInt)
                    .Value = paymentId;
                cmd.Parameters.Add("@SubscriptionId", SqlDbType.BigInt)
                    .Value = subscriptionId;

                await cmd.ExecuteNonQueryAsync();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<PaymentVerificationResult> VerifyAndActivateAsync(
            Guid adminId,
            VerifyPaymentDto dto,
            IRazorpayService razorpay)
        {
            long paymentId, subscriptionId;
            decimal amount;

            using (var con = Connection())
            {
                await con.OpenAsync();

                using var cmd = new SqlCommand(@"
    SELECT PaymentId, SubscriptionId, Amount, PaymentStatus
    FROM dbo.SubscriptionPayments
    WHERE GatewayOrderId = @OrderId
      AND AdminUserId = @AdminId
      AND GatewayName = 'Razorpay';", con);

                cmd.Parameters.Add("@OrderId", SqlDbType.NVarChar, 150)
                    .Value = dto.RazorpayOrderId;

                cmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                    .Value = adminId;

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("Payment order was not found.");

                paymentId = reader.GetInt64(0);
                subscriptionId = reader.GetInt64(1);
                amount = reader.GetDecimal(2);

                var paymentStatus = reader.GetString(3);

                if (paymentStatus is not ("Pending" or "Created" or "Success"))
                    throw new InvalidOperationException(
                        "This payment cannot be verified because it is no longer pending.");

                await reader.CloseAsync();

                // Browser callback data is not proof of payment. Verify the
                // HMAC signature and confirm captured status/amount with Razorpay.
                if (!razorpay.VerifyPaymentSignature(
                        dto.RazorpayOrderId,
                        dto.RazorpayPaymentId,
                        dto.RazorpaySignature))
                {
                    throw new InvalidOperationException(
                        "Razorpay payment signature is invalid.");
                }

                if (!await razorpay.IsPaymentCapturedAsync(
                        dto.RazorpayPaymentId, amount))
                {
                    throw new InvalidOperationException(
                        "Razorpay payment is not captured or the amount does not match.");
                }

                using var connection = Connection();
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction(
                IsolationLevel.Serializable);

            try
            {
                // Idempotency: repeated verification must not extend twice.
                using var existingCmd = new SqlCommand(@"
                    SELECT PaymentStatus
                    FROM dbo.SubscriptionPayments WITH (UPDLOCK, HOLDLOCK)
                    WHERE PaymentId = @PaymentId
                      AND AdminUserId = @AdminId;", connection, transaction);

                existingCmd.Parameters.Add("@PaymentId", SqlDbType.BigInt)
                    .Value = paymentId;
                existingCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                    .Value = adminId;

                var currentStatus = Convert.ToString(
                    await existingCmd.ExecuteScalarAsync());

                if (currentStatus == "Success")
                {
                    using var existingResult = new SqlCommand(@"
                        SELECT s.SubscriptionStatus, s.StartDate, s.EndDate
                        FROM dbo.AdminSubscriptions s
                        WHERE s.SubscriptionId = @SubscriptionId;",
                        connection, transaction);

                    existingResult.Parameters.Add("@SubscriptionId", SqlDbType.BigInt)
                        .Value = subscriptionId;

                    using var r = await existingResult.ExecuteReaderAsync();
                    await r.ReadAsync();

                    var already = new PaymentVerificationResult
                    {
                        PaymentId = paymentId,
                        SubscriptionId = subscriptionId,
                        PaymentStatus = "Success",
                        SubscriptionStatus = r.GetString(0),
                        SubscriptionStartDate = r.IsDBNull(1)
                            ? null : r.GetDateTime(1),
                        SubscriptionEndDate = r.IsDBNull(2)
                            ? null : r.GetDateTime(2)
                    };

                    await r.CloseAsync();
                    transaction.Commit();
                    return already;
                }

                // Load the pending subscription.
                using var subCmd = new SqlCommand(@"
                    SELECT AdminUserId, PlanCategory, DurationDays
                    FROM dbo.AdminSubscriptions WITH (UPDLOCK, HOLDLOCK)
                    WHERE SubscriptionId = @SubscriptionId
                      AND AdminUserId = @AdminId
                      AND SubscriptionStatus = 'Pending';",
                    connection, transaction);

                subCmd.Parameters.Add("@SubscriptionId", SqlDbType.BigInt)
                    .Value = subscriptionId;
                subCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                    .Value = adminId;

                Guid storedAdminId;
                int category, days;

                using (var r = await subCmd.ExecuteReaderAsync())
                {
                    if (!await r.ReadAsync())
                        throw new InvalidOperationException(
                            "Pending subscription was not found.");

                    storedAdminId = r.GetGuid(0);
                    category = r.GetByte(1);
                    days = r.GetInt32(2);
                }

                if (storedAdminId != adminId)
                    throw new InvalidOperationException("Admin mismatch.");

                // Extend from the latest active subscription of this category.
                using var dateCmd = new SqlCommand(@"
                    SELECT MAX(EndDate)
                    FROM dbo.AdminSubscriptions WITH (UPDLOCK, HOLDLOCK)
                    WHERE AdminUserId = @AdminId
                      AND PlanCategory = @Category
                      AND SubscriptionStatus = 'Active'
                      AND EndDate > SYSUTCDATETIME();",
                    connection, transaction);

                dateCmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                    .Value = adminId;
                dateCmd.Parameters.Add("@Category", SqlDbType.TinyInt)
                    .Value = category;

                var currentEnd = await dateCmd.ExecuteScalarAsync();

                var now = DateTime.UtcNow;
                var startDate = currentEnd == DBNull.Value || currentEnd == null ? now : Convert.ToDateTime(currentEnd);

                var endDate = startDate.AddDays(days);

                using var activateCmd = new SqlCommand(@"
                    UPDATE dbo.AdminSubscriptions
                    SET StartDate = @StartDate,
                        EndDate = @EndDate,
                        SubscriptionStatus = 'Active',
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE SubscriptionId = @SubscriptionId;

                    UPDATE dbo.SubscriptionPayments
                    SET GatewayPaymentId = @PaymentId,
                        GatewaySignature = @Signature,
                        TransactionReference = @PaymentId,
                        PaymentStatus = 'Success',
                        PaidAt = SYSUTCDATETIME(),
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE PaymentId = @LocalPaymentId;

                    UPDATE dbo.PromoCodeRedemptions
                    SET RedemptionStatus = 'Success',
                        RedeemedAt = SYSUTCDATETIME()
                    WHERE SubscriptionId = @SubscriptionId
                      AND RedemptionStatus = 'Pending';

                    UPDATE pc
                    SET pc.CurrentUsage = pc.CurrentUsage + 1,
                        pc.UpdatedAt = SYSUTCDATETIME()
                    FROM dbo.PromoCodes pc
                    INNER JOIN dbo.PromoCodeRedemptions pr
                        ON pr.PromoCodeId = pc.PromoCodeId
                    WHERE pr.SubscriptionId = @SubscriptionId
                      AND pr.RedemptionStatus = 'Success';",
                    connection, transaction);

                activateCmd.Parameters.Add("@StartDate", SqlDbType.DateTime2).Value = startDate;
                activateCmd.Parameters.Add("@EndDate", SqlDbType.DateTime2).Value = endDate;
                activateCmd.Parameters.Add("@SubscriptionId", SqlDbType.BigInt).Value = subscriptionId;
                    activateCmd.Parameters.Add("@PaymentId", SqlDbType.NVarChar, 150)
        .Value = dto.RazorpayPaymentId;

                    activateCmd.Parameters.Add("@Signature", SqlDbType.NVarChar, 500)
                        .Value = dto.RazorpaySignature;
                    activateCmd.Parameters.Add("@LocalPaymentId", SqlDbType.BigInt).Value = paymentId;
                await activateCmd.ExecuteNonQueryAsync();
                transaction.Commit();
                return new PaymentVerificationResult
                {
                    PaymentId = paymentId,
                    SubscriptionId = subscriptionId,
                    PaymentStatus = "Success",
                    SubscriptionStatus = "Active",
                    SubscriptionStartDate = startDate,
                    SubscriptionEndDate = endDate
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            } // closes initial order lookup connection scope
        }

        public async Task<List<SubscriptionHistoryDto>> GetSubscriptionHistoryAsync(Guid adminId)
        {
            var result = new List<SubscriptionHistoryDto>();

            using var con = Connection();
            await con.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT SubscriptionId, PlanName, PlanCategory,
                       BaseAmount, DiscountAmount, TaxAmount, FinalAmount,
                       SubscriptionStatus, StartDate, EndDate, CreatedAt
                FROM dbo.AdminSubscriptions
                WHERE AdminUserId = @AdminId
                ORDER BY CreatedAt DESC;", con);

            cmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                .Value = adminId;

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new SubscriptionHistoryDto
                {
                    SubscriptionId = reader.GetInt64(0),
                    PlanName = reader.GetString(1),
                    PlanCategory = reader.GetByte(2),
                    BaseAmount = reader.GetDecimal(3),
                    DiscountAmount = reader.GetDecimal(4),
                    TaxAmount = reader.GetDecimal(5),
                    FinalAmount = reader.GetDecimal(6),
                    SubscriptionStatus = reader.GetString(7),
                    StartDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    EndDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    CreatedAt = reader.GetDateTime(10)
                });
            }

            return result;
        }

        public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid adminId)
        {
            var result = new List<PaymentHistoryDto>();

            using var con = Connection();
            await con.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT PaymentId, SubscriptionId, GatewayOrderId,
                       GatewayPaymentId, Amount, Currency, PaymentStatus,
                       PaymentMethod, FailureReason, CreatedAt, PaidAt
                FROM dbo.SubscriptionPayments
                WHERE AdminUserId = @AdminId
                ORDER BY CreatedAt DESC;", con);

            cmd.Parameters.Add("@AdminId", SqlDbType.UniqueIdentifier)
                .Value = adminId;

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new PaymentHistoryDto
                {
                    PaymentId = reader.GetInt64(0),
                    SubscriptionId = reader.GetInt64(1),
                    GatewayOrderId = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    GatewayPaymentId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Amount = reader.GetDecimal(4),
                    Currency = reader.GetString(5).Trim(),
                    PaymentStatus = reader.GetString(6),
                    PaymentMethod = reader.IsDBNull(7) ? null : reader.GetString(7),
                    FailureReason = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9),
                    PaidAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                });
            }

            return result;
        }

        private static void AddMoney(
            SqlCommand command, string name, decimal value)
        {
            var parameter = command.Parameters.Add(
                name, SqlDbType.Decimal);

            parameter.Precision = 18;
            parameter.Scale = 2;
            parameter.Value = value;
        }
    }
}