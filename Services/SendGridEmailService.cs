using CRUDAppUsingADO.Models.Bulk;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CRUDAppUsingADO.Services;

public sealed class SendGridEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SendGridEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DeliveryResult> SendAsync(
        string recipientName,
        string recipientEmail,
        string message,
        string? fileUrl,
        string? generatedRelativePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return new DeliveryResult
                {
                    Success = false,
                    Response = "Recipient email is missing."
                };
            }

            string apiKey = _configuration["SendGrid:ApiKey"] ?? "";
            string fromEmail = _configuration["SendGrid:FromEmail"] ?? "";
            string fromName = _configuration["SendGrid:FromName"] ?? "Certwala";

            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                return new DeliveryResult
                {
                    Success = false,
                    Response = "SendGrid configuration is incomplete."
                };
            }

            string safeName = HtmlEncoder.Default.Encode(
                string.IsNullOrWhiteSpace(recipientName)
                    ? "Customer"
                    : recipientName);

            string safeMessage = HtmlEncoder.Default.Encode(message ?? "")
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>");

            string html = $"""
                <div style="font-family:Arial,sans-serif;font-size:14px">
                    <p>Dear {safeName},</p>
                    <p>{safeMessage}</p>
                    {(string.IsNullOrWhiteSpace(fileUrl) ? "" :
                        $"""<p><a href="{HtmlEncoder.Default.Encode(fileUrl)}">View your card / document</a></p>""")}
                    <p>Regards,<br/>{HtmlEncoder.Default.Encode(fromName)}</p>
                </div>
                """;

            string plainText =
                $"Dear {recipientName},\n\n{message}\n\n" +
                (string.IsNullOrWhiteSpace(fileUrl)
                    ? ""
                    : $"View your card / document: {fileUrl}\n\n") +
                $"Regards,\n{fromName}";

            var emailContent = new List<object>
            {
                new { type = "text/plain", value = plainText },
                new { type = "text/html", value = html }
            };

            var personalization = new
            {
                to = new[]
                {
                    new
                    {
                        email = recipientEmail,
                        name = recipientName
                    }
                }
            };

            var payload = new Dictionary<string, object>
            {
                ["personalizations"] = new[] { personalization },
                ["from"] = new
                {
                    email = fromEmail,
                    name = fromName
                },
                ["subject"] = $"Your card / document from {fromName}",
                ["content"] = emailContent
            };

            // Attach a generated file when its path is available.
            string? physicalPath = GetSafePhysicalPath(generatedRelativePath);

            if (!string.IsNullOrWhiteSpace(physicalPath) &&
                File.Exists(physicalPath))
            {
                byte[] bytes = await File.ReadAllBytesAsync(
                    physicalPath,
                    cancellationToken);

                string extension = Path.GetExtension(physicalPath).ToLowerInvariant();

                string mimeType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };

                payload["attachments"] = new[]
                {
                    new
                    {
                        content = Convert.ToBase64String(bytes),
                        type = mimeType,
                        filename = Path.GetFileName(physicalPath),
                        disposition = "attachment"
                    }
                };
            }

            var client = _httpClientFactory.CreateClient("SendGrid");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.sendgrid.com/v3/mail/send");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            string responseText = await response.Content.ReadAsStringAsync(
                cancellationToken);

            // SendGrid normally returns HTTP 202 when it accepts the message.
            bool accepted = response.StatusCode ==
                            System.Net.HttpStatusCode.Accepted;

            if (!accepted)
            {
                _logger.LogWarning(
                    "SendGrid rejected email. HTTP {StatusCode}.",
                    (int)response.StatusCode);
            }

            return new DeliveryResult
            {
                Success = accepted,
                Response = string.IsNullOrWhiteSpace(responseText)
                    ? $"HTTP {(int)response.StatusCode}"
                    : responseText
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid email delivery failed.");

            return new DeliveryResult
            {
                Success = false,
                Response = ex.Message
            };
        }
    }

    private string? GetSafePhysicalPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        string webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        string relative = relativePath.TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar);

        string rootFullPath = Path.GetFullPath(webRoot);
        string fullPath = Path.GetFullPath(
            Path.Combine(rootFullPath, relative));

        string rootWithSeparator =
            rootFullPath.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid generated file path.");
        }

        return fullPath;
    }
}