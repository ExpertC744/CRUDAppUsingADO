using CRUDAppUsingADO.Models.Bulk;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http;

namespace CRUDAppUsingADO.Services;

public sealed class BhashWhatsAppService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BhashWhatsAppService> _logger;

    public BhashWhatsAppService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<BhashWhatsAppService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DeliveryResult> SendWhatsAppAsync(
        int adminId,
        string name,
        string mobile,
        string templateName,
        IReadOnlyList<string>? parameters,
        int utilMark,
        string? templateType,
        string? mediaUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return new DeliveryResult
                {
                    Success = false,
                    Response = "WhatsApp template name is missing."
                };

            string phone = NormalizeIndianMobile(mobile);

            if (phone.Length != 12 || !phone.StartsWith("91"))
                return new DeliveryResult
                {
                    Success = false,
                    Response = "Invalid Indian mobile number."
                };

            string endpointKey = utilMark == 1
                ? "BhashSMS:UtilityEndpoint"
                : "BhashSMS:MarketingEndpoint";

            string endpoint = _configuration[endpointKey] ?? "";

            if (string.IsNullOrWhiteSpace(endpoint))
                return new DeliveryResult
                {
                    Success = false,
                    Response = $"Configuration {endpointKey} is missing."
                };

            // Preserve the old Certwala account-selection logic.
            string accountPath = adminId switch
            {
                4885 => "BhashSMS:AdminAccounts:4885",
                2393 => "BhashSMS:AdminAccounts:2393",
                5887 => "BhashSMS:AdminAccounts:5887",
                _ => "BhashSMS:Default"
            };

            string user = _configuration[$"{accountPath}:User"] ?? "";
            string password = _configuration[$"{accountPath}:Password"] ?? "";
            string sender = _configuration["BhashSMS:Sender"] ?? "";

            if (string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(sender))
            {
                return new DeliveryResult
                {
                    Success = false,
                    Response = $"BhashSMS account configuration is incomplete for Admin ID {adminId}."
                };
            }

            var query = new Dictionary<string, string?>
            {
                ["user"] = user,
                ["pass"] = password,
                ["sender"] = sender,
                ["phone"] = phone,
                ["text"] = templateName,
                ["priority"] = "wa",
                ["stype"] = "normal"
            };

            // Keep the parameter order because {{1}}, {{2}}, etc. are positional.
            if (parameters is { Count: > 0 })
            {
                query["Params"] = string.Join(
                    ",",
                    parameters.Select(p => p?.Trim() ?? ""));
            }

            AddMediaParameters(query, name, templateType, mediaUrl);

            string requestUrl = QueryHelpers.AddQueryString(endpoint, query);

            var client = _httpClientFactory.CreateClient("BhashSMS");

            using var response = await client.GetAsync(
                requestUrl,
                cancellationToken);

            string responseText = await response.Content.ReadAsStringAsync(
                cancellationToken);

            responseText = responseText.Trim();

            // Old Certwala example returned responses such as S.44037.
            // Do not treat every non-empty response as a successful send.
            bool providerAccepted =
                response.IsSuccessStatusCode &&
                responseText.StartsWith("S.", StringComparison.OrdinalIgnoreCase);

            if (!providerAccepted)
            {
                _logger.LogWarning(
                    "BhashSMS rejected message for AdminId {AdminId}. HTTP {StatusCode}.",
                    adminId,
                    (int)response.StatusCode);
            }

            return new DeliveryResult
            {
                Success = providerAccepted,
                Response = responseText
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "BhashSMS delivery failed for AdminId {AdminId}.",
                adminId);

            return new DeliveryResult
            {
                Success = false,
                Response = ex.Message
            };
        }
    }

    private static string NormalizeIndianMobile(string? mobile)
    {
        string phone = new string(
            (mobile ?? "").Where(char.IsDigit).ToArray());

        if (phone.StartsWith("0"))
            phone = phone[1..];

        if (phone.Length == 10)
            phone = "91" + phone;

        return phone;
    }

    private static void AddMediaParameters(
        Dictionary<string, string?> query,
        string? recipientName,
        string? templateType,
        string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(templateType) ||
            string.IsNullOrWhiteSpace(mediaUrl))
            return;

        switch (templateType.Trim().ToLowerInvariant())
        {
            case "textimage":
            case "textbuttonimage":
                query["htype"] = "image";
                query["url"] = mediaUrl;
                break;

            case "textvideo":
            case "textbuttonvideo":
                query["htype"] = "video";
                query["url"] = mediaUrl;
                break;

            case "textdocument":
            case "textpdf":
                query["htype"] = "document";
                query["fname"] = string.IsNullOrWhiteSpace(recipientName)
                    ? "Card.pdf"
                    : recipientName.Trim() + ".pdf";
                query["url"] = mediaUrl;
                break;
        }
    }
}