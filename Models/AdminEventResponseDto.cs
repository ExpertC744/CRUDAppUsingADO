namespace CRUDAppUsingADO.Models
{
    public class AdminEventResponseDto
    {
        public int EventId { get; set; }
        public Guid AdminUserId { get; set; }

        public string OrganizationName { get; set; } = "";
        public string EventTitle { get; set; } = "";

        public DateTime AdvStartDate { get; set; }
        public DateTime AdvCompletionDate { get; set; }

        public DateTime EventStartDate { get; set; }
        public DateTime EventCompletionDate { get; set; }

        public string EventTime { get; set; } = "";

        public string Venue { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Country { get; set; } = "";

        public string Image1 { get; set; } = "";
        public string? Image2 { get; set; }

        public string AboutEvent { get; set; } = "";
        public string Instructions { get; set; } = "";

        public decimal EntryFee { get; set; }
        public string PaymentMode { get; set; } = "";

        public bool GPay { get; set; }
        public bool PhonePe { get; set; }
        public bool Paytm { get; set; }

        public string? UPIID { get; set; }
        public string? PaymentMobileNo { get; set; }
        public string? QRCodeUrl { get; set; }

        public string? BankName { get; set; }
        public string? AccountHolderName { get; set; }
        public string? AccountNumber { get; set; }
        public string? IFSCCode { get; set; }
        public string? BranchName { get; set; }

        public bool nBit { get; set; }
        public bool nSABit { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}