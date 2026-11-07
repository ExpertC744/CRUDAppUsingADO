namespace CRUDAppUsingADO.Models.Configuration
{
    public class PaymentGatewaySettings
    {
        public string KeyId { get; set; } = "";
        public string KeySecret { get; set; } = "";
        public string WebhookSecret { get; set; } = "";
        public string Currency { get; set; } = "INR";
    }
}