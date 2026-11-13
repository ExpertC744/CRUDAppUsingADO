using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models
{
    public class AdminEventSaveDto : IValidatableObject
    {
        [Required, StringLength(200)]
        public string OrganizationName { get; set; } = "";

        [Required, StringLength(250)]
        public string EventTitle { get; set; } = "";

        [Required]
        public DateTime AdvStartDate { get; set; }

        [Required]
        public DateTime AdvCompletionDate { get; set; }

        [Required]
        public DateTime EventStartDate { get; set; }

        [Required]
        public DateTime EventCompletionDate { get; set; }

        [Required, StringLength(100)]
        public string EventTime { get; set; } = "";

        [Required, StringLength(300)]
        public string Venue { get; set; } = "";

        [Required, StringLength(100)]
        public string City { get; set; } = "";

        [Required, StringLength(100)]
        public string State { get; set; } = "";

        [Required, StringLength(100)]
        public string Country { get; set; } = "";

        [Required, StringLength(500)]
        public string AboutEvent { get; set; } = "";

        [Required, StringLength(500)]
        public string Instructions { get; set; } = "";

        [Range(0, 99999999.99)]
        public decimal EntryFee { get; set; }

        [Required]
        [RegularExpression("^(Cash|Online)$",
            ErrorMessage = "Payment Mode must be Cash or Online.")]
        public string PaymentMode { get; set; } = "Cash";

        public bool GPay { get; set; }
        public bool PhonePe { get; set; }
        public bool Paytm { get; set; }

        [StringLength(150)]
        public string? UPIID { get; set; }

        [Phone]
        [StringLength(20)]
        public string? PaymentMobileNo { get; set; }

        [StringLength(150)]
        public string? BankName { get; set; }

        [StringLength(150)]
        public string? AccountHolderName { get; set; }

        [StringLength(50)]
        public string? AccountNumber { get; set; }

        [StringLength(20)]
        public string? IFSCCode { get; set; }

        [StringLength(150)]
        public string? BranchName { get; set; }

        // Required when creating; optional when editing.
        public IFormFile? Image1 { get; set; }

        public IFormFile? Image2 { get; set; }

        public IFormFile? QRCode { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {

            if (AdvStartDate == DateTime.MinValue)
                yield return new ValidationResult(
                    "Advertisement start date is required.",
                    new[] { nameof(AdvStartDate) });

            if (AdvCompletionDate == DateTime.MinValue)
                yield return new ValidationResult(
                    "Advertisement completion date is required.",
                    new[] { nameof(AdvCompletionDate) });

            if (EventStartDate == DateTime.MinValue)
                yield return new ValidationResult(
                    "Event start date is required.",
                    new[] { nameof(EventStartDate) });

            if (EventCompletionDate == DateTime.MinValue)
                yield return new ValidationResult(
                    "Event completion date is required.",
                    new[] { nameof(EventCompletionDate) });

            if (AdvCompletionDate.Date < AdvStartDate.Date)
            {
                yield return new ValidationResult(
                    "Advertisement completion date must be on or after advertisement start date.",
                    new[] { nameof(AdvCompletionDate) });
            }

            if (EventCompletionDate.Date < EventStartDate.Date)
            {
                yield return new ValidationResult(
                    "Event completion date must be on or after event start date.",
                    new[] { nameof(EventCompletionDate) });
            }

            if (PaymentMode == "Online")
            {
                if (!GPay && !PhonePe && !Paytm &&
                    string.IsNullOrWhiteSpace(UPIID) &&
                    string.IsNullOrWhiteSpace(BankName))
                {
                    yield return new ValidationResult(
                        "For online payment, select a payment method or provide bank details.",
                        new[] { nameof(PaymentMode) });
                }

                if (GPay || PhonePe || Paytm)
                {
                    if (string.IsNullOrWhiteSpace(UPIID) &&
                        string.IsNullOrWhiteSpace(PaymentMobileNo))
                    {
                        yield return new ValidationResult(
                            "Provide UPI ID or payment mobile number.",
                            new[] { nameof(UPIID), nameof(PaymentMobileNo) });
                    }
                }
            }
        }
    }
}