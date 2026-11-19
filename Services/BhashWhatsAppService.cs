
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
                return Fail("WhatsApp template name is missing.");
            }

            string phone = NormalizeMobile(mobile);

            if (phone.Length < 10)
            {
                return Fail("Invalid mobile number.");
            }

            // 1 = Utility, 2 = Marketing
            string endpointKey = utilMark == 1
                ? "BhashSMS:UtilityEndpoint"
                : "BhashSMS:MarketingEndpoint";

            string endpoint = _configuration[endpointKey] ?? "";

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return Fail($"BhashSMS endpoint missing: {endpointKey}");
            }

            // Resolve admin-specific account.
            string? accountKey =
                _configuration[$"BhashSMS:AdminAccountMap:{adminUserId:D}"];

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
                return Fail("BhashSMS account configuration is incomplete.");
            }

            // 2. Prepare BhashSMS API parameters.
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

            // 3. Template parameters.
            if (parameters is { Count: > 0 })
            {
                query["Params"] = string.Join(
                    ",",
                    parameters.Select(x => x?.Trim() ?? ""));
            }

            // 4. Image / Video / Document.
            AddMediaParameters(
                query,
                name,
                templateType,
                mediaUrl);

            // 5. Generate API request URL.
            string requestUrl =
                QueryHelpers.AddQueryString(endpoint, query);

            var client = _httpClientFactory.CreateClient("BhashSMS");

            _logger.LogInformation(
                "BhashSMS request: Phone={Phone}, Template={Template}, Endpoint={Endpoint}, MediaType={MediaType}, HasMedia={HasMedia}",
                phone,
                templateName,
                endpointKey,
                templateType,
                !string.IsNullOrWhiteSpace(mediaUrl));

            using var response = await client.GetAsync(
                requestUrl,
                cancellationToken);

            string responseText =
                (await response.Content.ReadAsStringAsync(
                    cancellationToken)).Trim();

            bool success =
                response.IsSuccessStatusCode &&
                responseText.StartsWith(
                    "S.",
                    StringComparison.OrdinalIgnoreCase);

            if (!success)
            {
                _logger.LogWarning(
                    "BhashSMS failed. HTTP={StatusCode}, Response={Response}",
                    (int)response.StatusCode,
                    responseText);
            }
            else
            {
                _logger.LogInformation(
                    "BhashSMS accepted message. Response={Response}",
                    responseText);
            }

            return new DeliveryResult
            {
                Success = success,
                Response = responseText
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BhashSMS delivery exception.");

            return Fail(ex.Message);
        }
    }

    private static DeliveryResult Fail(string message)
    {
        return new DeliveryResult
        {
            Success = false,
            Response = message
        };
    }

    private static string NormalizeMobile(string? mobile)
    {
        string phone = new string(
            (mobile ?? "").Where(char.IsDigit).ToArray());

        if (phone.Length == 12 && phone.StartsWith("91"))
            phone = phone.Substring(2);

        if (phone.Length == 11 && phone.StartsWith("0"))
            phone = phone.Substring(1);

        return phone;
    }

    private static void AddMediaParameters(
        Dictionary<string, string?> query,
        string? recipientName,
        string? templateType,
        string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(templateType))
            return;

        switch (templateType.Trim().ToLowerInvariant())
        {
            case "textimage":
            case "textbuttonimage":

                if (string.IsNullOrWhiteSpace(mediaUrl))
                    throw new InvalidOperationException(
                        "Image URL is missing for WhatsApp image message.");

                query["htype"] = "image";
                query["url"] = mediaUrl;
                break;

            case "textvideo":
            case "textbuttonvideo":

                if (string.IsNullOrWhiteSpace(mediaUrl))
                    throw new InvalidOperationException(
                        "Video URL is missing for WhatsApp video message.");

                query["htype"] = "video";
                query["url"] = mediaUrl;
                break;

            case "textdocument":
            case "textpdf":

                if (string.IsNullOrWhiteSpace(mediaUrl))
                    throw new InvalidOperationException(
                        "Document URL is missing for WhatsApp document message.");

                query["htype"] = "document";
                query["fname"] =
                    string.IsNullOrWhiteSpace(recipientName)
                        ? "Card.pdf"
                        : recipientName.Trim() + ".pdf";

                query["url"] = mediaUrl;
                break;
        }
    }
}
