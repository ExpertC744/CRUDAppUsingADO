
using CRUDAppUsingADO.Models.Bulk;
using Microsoft.AspNetCore.WebUtilities;

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
        Guid adminUserId,
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
            {
                return new DeliveryResult
                {
                    Success = false,
                    Response = "WhatsApp template name is missing."
                };
            }

            string phone = NormalizeMobile(mobile);

            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
            {
                return new DeliveryResult
                {
                    Success = false,
                    Response = "Invalid mobile number."
                };
            }

            // TemplateType: 1 = Utility, 2 = Marketing.
            string endpointKey = utilMark == 1
                ? "BhashSMS:UtilityEndpoint"
                : "BhashSMS:MarketingEndpoint";

            string endpoint = _configuration[endpointKey] ?? "";

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return new DeliveryResult
                {
                    Success = false,
                    Response = $"Configuration {endpointKey} is missing."
                };
            }

            // Find the numeric BhashSMS account key configured for this admin GUID.
            string? accountKey = _configuration[$"BhashSMS:AdminAccountMap:{adminUserId:D}"];

            // If no mapping exists, use the default BhashSMS account.
            string accountPath = !string.IsNullOrWhiteSpace(accountKey)
                ? $"BhashSMS:AdminAccounts:{accountKey}"
                : "BhashSMS:Default";

            string user = _configuration[$"{accountPath}:User"] ?? "";
            string password = _configuration[$"{accountPath}:Password"] ?? "";
            string sender = _configuration["BhashSMS:Sender"] ?? "";

            if (string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(sender))
            {
                _logger.LogError(
                    "BhashSMS account configuration is incomplete for AdminUserId {AdminUserId}. AccountPath={AccountPath}",
                    adminUserId,
                    accountPath);

                return new DeliveryResult
                {
                    Success = false,
                    Response = $"BhashSMS account configuration is incomplete for AdminUserId {adminUserId}."
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

            // Keep parameter order because {{1}}, {{2}}, etc. are positional.
            if (parameters is { Count: > 0 })
            {
                query["Params"] = string.Join(
                    ",",
                    parameters.Select(p => p?.Trim() ?? ""));
            }

            AddMediaParameters(
                query,
                name,
                templateType,
                mediaUrl);

            string requestUrl = QueryHelpers.AddQueryString(
                endpoint,
                query);

            var client = _httpClientFactory.CreateClient("BhashSMS");

            _logger.LogInformation(
                "Sending WhatsApp message. AdminUserId={AdminUserId}, Phone={Phone}, Template={TemplateName}, AccountPath={AccountPath}",
                adminUserId,
                phone,
                templateName,
                accountPath);

            using var response = await client.GetAsync(
                requestUrl,
                cancellationToken);

            string responseText =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            responseText = responseText.Trim();

            // BhashSMS accepted response example: S.44037
            bool providerAccepted =
                response.IsSuccessStatusCode &&
                responseText.StartsWith(
                    "S.",
                    StringComparison.OrdinalIgnoreCase);

            if (!providerAccepted)
            {
                _logger.LogWarning(
                    "BhashSMS rejected message for AdminUserId {AdminUserId}. HTTP {StatusCode}. Response={Response}",
                    adminUserId,
                    (int)response.StatusCode,
                    responseText);
            }
            else
            {
                _logger.LogInformation(
                    "BhashSMS accepted message for AdminUserId {AdminUserId}. Response={Response}",
                    adminUserId,
                    responseText);
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
            _logger.LogError(ex, "BhashSMS delivery failed for AdminUserId {AdminUserId}.", adminUserId);

            return new DeliveryResult
            {
                Success = false,
                Response = ex.Message
            };
        }
    }

    private static string NormalizeMobile(string? mobile)
    {
        // Keep only numeric digits.
        return new string((mobile ?? "").Where(char.IsDigit).ToArray());
    }

    private static void AddMediaParameters(
        Dictionary<string, string?> query,
        string? recipientName,
        string? templateType,
        string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(templateType) || string.IsNullOrWhiteSpace(mediaUrl))
        {
            return;
        }

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
