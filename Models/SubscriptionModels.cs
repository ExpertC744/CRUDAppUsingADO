namespace CRUDAppUsingADO.Models
{
    public static class SubscriptionConstants
    {
        public const decimal GstPercentage = 18m;

        public const int Events = 1;
        public const int Creatives = 2;
        public const int EventsAndCreatives = 3;
        public const int Download = 4;

        // Promo visibility:
        // 1 = Single/private (not shown publicly)
        // 2 = Multiple/public (shown publicly)
        public const int PrivatePromo = 1;
        public const int PublicPromo = 2;

        // Discount type:
        // 1 = Percentage
        // 2 = Fixed amount
        public const int PercentageDiscount = 1;
        public const int FixedDiscount = 2;
    }

    public class SubscriptionPlanDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = "";
        public int PlanCategory { get; set; }
        public int DurationValue { get; set; }
        public string DurationUnit { get; set; } = "";
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "INR";

        // Always 18%; returned for display only.
        public decimal TaxPercentage { get; set; } =
            SubscriptionConstants.GstPercentage;

        public int Quantity { get; set; }
        public int EventQuantity { get; set; }
        public int DownloadQuantity { get; set; }
        public string? Heading1 { get; set; }
        public string? Content1 { get; set; }
        public string? Heading2 { get; set; }
        public string? Content2 { get; set; }
        public bool IsPopular { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreatePlanDto
    {
        public string PlanName { get; set; } = "";
        public int PlanCategory { get; set; }
        public int DurationValue { get; set; }
        public string DurationUnit { get; set; } = "Day";
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "INR";

        // Ignored by backend; GST is fixed at 18%.
        public decimal TaxPercentage { get; set; } =
            SubscriptionConstants.GstPercentage;

        public int Quantity { get; set; }
        public int EventQuantity { get; set; }
        public int DownloadQuantity { get; set; }
        public string? Heading1 { get; set; }
        public string? Content1 { get; set; }
        public string? Heading2 { get; set; }
        public string? Content2 { get; set; }
        public bool IsPopular { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class PromoCodeDto
    {
        public int PromoCodeId { get; set; }
        public string PromoCode { get; set; } = "";
        public string PromoName { get; set; } = "";

        // 1 = private, 2 = public
        public int VisibilityType { get; set; }

        // 1 = percentage, 2 = fixed amount
        public int DiscountType { get; set; }

        public decimal DiscountValue { get; set; }
        public decimal? MaximumDiscount { get; set; }
        public decimal MinimumOrderAmount { get; set; }
        public int? ApplicableCategory { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int? MaximumUsage { get; set; }
        public int? MaximumUsagePerAdmin { get; set; }
        public int CurrentUsage { get; set; }
        public bool IsActive { get; set; }
        public List<int> PlanIds { get; set; } = new();
    }

    public class CreatePromoCodeDto
    {
        public string PromoCode { get; set; } = "";
        public string PromoName { get; set; } = "";
        public int VisibilityType { get; set; }
        public int DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaximumDiscount { get; set; }
        public decimal MinimumOrderAmount { get; set; }
        public int? ApplicableCategory { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int? MaximumUsage { get; set; }
        public int? MaximumUsagePerAdmin { get; set; }
        public List<int> PlanIds { get; set; } = new();
    }

    // Use this DTO for public promo-code validation.
    public class ValidatePromoCodeDto
    {
        public int PlanId { get; set; }
        public string PromoCode { get; set; } = "";
    }


    // Standard response envelope used by subscription endpoints.
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }
    }

    public class SubscriptionStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class CheckoutRequestDto
    {
        public int PlanId { get; set; }
        public string? PromoCode { get; set; }
    }

    public class CheckoutResponseDto
    {
        public long PaymentId { get; set; }
        public long SubscriptionId { get; set; }
        public string RazorpayOrderId { get; set; } = "";
        public string RazorpayKeyId { get; set; } = "";
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Currency { get; set; } = "INR";
        public string PlanName { get; set; } = "";
    }

    public class VerifyPaymentDto
    {
        public string RazorpayOrderId { get; set; } = "";
        public string RazorpayPaymentId { get; set; } = "";
        public string RazorpaySignature { get; set; } = "";
    }

    public class PaymentVerificationResult
    {
        public long PaymentId { get; set; }
        public long SubscriptionId { get; set; }
        public string PaymentStatus { get; set; } = "";
        public string SubscriptionStatus { get; set; } = "";
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
    }

    public class PriceCalculationDto
    {
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }

    public class SubscriptionHistoryDto
    {
        public long SubscriptionId { get; set; }
        public string PlanName { get; set; } = "";
        public int PlanCategory { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string SubscriptionStatus { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentHistoryDto
    {
        public long PaymentId { get; set; }
        public long SubscriptionId { get; set; }
        public string GatewayOrderId { get; set; } = "";
        public string? GatewayPaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string PaymentStatus { get; set; } = "";
        public string? PaymentMethod { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
