namespace CRUDAppUsingADO.Services
{
    public interface IRazorpayService
    {
        Task<string> CreateOrderAsync(
            decimal amount,
            string receipt,
            CancellationToken cancellationToken = default);

        bool VerifyPaymentSignature(
            string orderId,
            string paymentId,
            string signature);

        Task<bool> IsPaymentCapturedAsync(
            string paymentId,
            decimal expectedAmount,
            CancellationToken cancellationToken = default);

        bool VerifyWebhookSignature(string body, string signature);
    }
}