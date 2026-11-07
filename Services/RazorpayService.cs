using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CRUDAppUsingADO.Models.Configuration;
using Microsoft.Extensions.Options;

namespace CRUDAppUsingADO.Services
{
    public class RazorpayService : IRazorpayService
    {
        private readonly HttpClient _httpClient;
        private readonly PaymentGatewaySettings _settings;

        public RazorpayService(
            HttpClient httpClient,
            IOptions<PaymentGatewaySettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{_settings.KeyId}:{_settings.KeySecret}"));

            _httpClient.BaseAddress =
                new Uri("https://api.razorpay.com/v1/");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        public async Task<string> CreateOrderAsync(
            decimal amount,
            string receipt,
            CancellationToken cancellationToken = default)
        {
            var amountInPaise = Convert.ToInt64(
                Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

            var request = new
            {
                amount = amountInPaise,
                currency = _settings.Currency,
                receipt = receipt
            };

            var json = JsonSerializer.Serialize(request);

            using var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(
                "orders", content, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Razorpay order creation failed: {responseBody}");
            }

            using var document = JsonDocument.Parse(responseBody);

            return document.RootElement
                .GetProperty("id")
                .GetString()!;
        }

        public bool VerifyPaymentSignature(
            string orderId,
            string paymentId,
            string signature)
        {
            if (string.IsNullOrWhiteSpace(orderId) ||
                string.IsNullOrWhiteSpace(paymentId) ||
                string.IsNullOrWhiteSpace(signature))
                return false;

            var payload = $"{orderId}|{paymentId}";

            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(_settings.KeySecret));

            var hash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(payload));

            var expectedSignature =
                Convert.ToHexString(hash).ToLowerInvariant();

            try
            {
                var expectedBytes =
                    Convert.FromHexString(expectedSignature);

                var actualBytes =
                    Convert.FromHexString(signature);

                return expectedBytes.Length == actualBytes.Length &&
                       CryptographicOperations.FixedTimeEquals(
                           expectedBytes, actualBytes);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsPaymentCapturedAsync(
            string paymentId,
            decimal expectedAmount,
            CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(
                $"payments/{Uri.EscapeDataString(paymentId)}",
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var status = root.GetProperty("status").GetString();

            var amountInPaise = root.GetProperty("amount").GetInt64();

            var expectedPaise = Convert.ToInt64(
                Math.Round(expectedAmount * 100m, 0,
                    MidpointRounding.AwayFromZero));

            return string.Equals(
                       status, "captured",
                       StringComparison.OrdinalIgnoreCase)
                   && amountInPaise == expectedPaise;
        }

        public bool VerifyWebhookSignature(
            string body,
            string signature)
        {
            if (string.IsNullOrWhiteSpace(_settings.WebhookSecret) ||
                string.IsNullOrWhiteSpace(signature))
                return false;

            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(_settings.WebhookSecret));

            var hash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(body));

            var expected = Convert.ToHexString(hash).ToLowerInvariant();

            try
            {
                var expectedBytes = Convert.FromHexString(expected);
                var actualBytes = Convert.FromHexString(signature);

                return expectedBytes.Length == actualBytes.Length &&
                       CryptographicOperations.FixedTimeEquals(
                           expectedBytes, actualBytes);
            }
            catch
            {
                return false;
            }
        }
    }
}