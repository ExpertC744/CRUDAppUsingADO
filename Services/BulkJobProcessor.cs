
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models.Bulk;
using System.Text;

namespace CRUDAppUsingADO.Services;

public sealed class BulkJobProcessor
{
    private readonly BulkCardDAL _dal;
    private readonly CardImageRenderer _renderer;
    private readonly BhashWhatsAppService _whatsAppService;
    private readonly SendGridEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BulkJobProcessor> _logger;

    public BulkJobProcessor(
        BulkCardDAL dal,
        CardImageRenderer renderer,
        BhashWhatsAppService whatsAppService,
        SendGridEmailService emailService,
        IConfiguration configuration,
        ILogger<BulkJobProcessor> logger)
    {
        _dal = dal;
        _renderer = renderer;
        _whatsAppService = whatsAppService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        // 1. Load job
        var job = await _dal.LoadJobAsync(jobId, ct);

        if (job == null)
            throw new InvalidOperationException("Bulk job not found.");

        // 2. Load pending recipients
        var recipients = await _dal.LoadJobRecipientsAsync(jobId, ct);

        _logger.LogInformation(
            "Starting bulk job {JobId}. Recipients: {Count}",
            jobId,
            recipients.Count);

        foreach (var recipient in recipients)
        {
            ct.ThrowIfCancellationRequested();

            string? generatedImage = null;

            try
            {
                // 3. Mark recipient as Processing
                await _dal.UpdateRecipientAsync(
                    jobId,
                    recipient.RecipientId,
                    "Processing",
                    null,
                    null,
                    ct);

                // 4. Validate output type
                if (!string.Equals(
                    job.OutputType,
                    "Image",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException(
                        $"Output type '{job.OutputType}' is not implemented. Currently only Image is supported.");
                }

                // 5. Generate personalized image
                generatedImage = await _renderer.GenerateAsync(
                    job,
                    recipient,
                    ct);

                if (string.IsNullOrWhiteSpace(generatedImage))
                {
                    throw new InvalidOperationException(
                        "Image renderer returned an empty image path.");
                }

                // 6. Read recipient details
                string recipientName = recipient.FullName ?? "";
                string recipientEmail = recipient.Email ?? "";
                string recipientMobile = recipient.Mobile ?? "";

                string channels = job.Channels ?? "";

                bool sendWhatsApp =
                    channels.Equals(
                        "WhatsApp",
                        StringComparison.OrdinalIgnoreCase) ||
                    channels.Equals(
                        "Both",
                        StringComparison.OrdinalIgnoreCase);

                bool sendEmail =
                    channels.Equals(
                        "Email",
                        StringComparison.OrdinalIgnoreCase) ||
                    channels.Equals(
                        "Both",
                        StringComparison.OrdinalIgnoreCase);

                if (!sendWhatsApp && !sendEmail)
                {
                    throw new NotSupportedException(
                        $"Unsupported delivery channel: {channels}");
                }

                // 7. Build public image URL
                string publicBaseUrl =
                    _configuration["Frontend:PublicUrl"]?.TrimEnd('/')
                    ?? throw new InvalidOperationException(
                        "Frontend:PublicUrl is missing from configuration.");

                string mediaUrl =
                    publicBaseUrl + "/" + generatedImage.TrimStart('/');

                // 8. Prepare WhatsApp template parameters
                var parameters = new List<string>
                {
                    recipient.Text1 ?? recipientName,
                    recipient.Text2 ?? "",
                    recipient.Text3 ?? "",
                    recipient.Text4 ?? ""
                };

                int parameterCount = Math.Clamp(
                    job.TemplateParameterCount,
                    0,
                    parameters.Count);

                parameters = parameters
                    .Take(parameterCount)
                    .ToList();

                // 9. Prepare template details
                string templateName = job.TemplateName ?? "";

                // CORRECT DATABASE MAPPING:
                //
                // TemplateType:
                // 1 = Utility
                // 2 = Marketing
                //
                // SendType:
                // 1 = Text
                // 2 = Text + Image
                // 3 = Text + Video
                // 4 = Text + Document

                // Select API endpoint using TemplateType
                int utilMark = job.TemplateType switch
                {
                    1 => 1, // Utility endpoint
                    2 => 0, // Marketing endpoint
                    _ => throw new InvalidOperationException(
                        $"Invalid TemplateType: {job.TemplateType}")
                };

                // Select message/media type using SendType
                string templateType = job.TemplateSendType switch
                {
                    1 => "text",
                    2 => "textimage",
                    3 => "textvideo",
                    4 => "textdocument",
                    _ => throw new InvalidOperationException(
                        $"Invalid SendType: {job.TemplateSendType}")
                };

                DeliveryResult? whatsappResult = null;
                DeliveryResult? emailResult = null;

                // 10. Send WhatsApp
                if (sendWhatsApp)
                {
                    if (string.IsNullOrWhiteSpace(recipientMobile))
                    {
                        whatsappResult = new DeliveryResult
                        {
                            Success = false,
                            Response = "Recipient mobile number is missing."
                        };
                    }
                    else if (string.IsNullOrWhiteSpace(templateName))
                    {
                        whatsappResult = new DeliveryResult
                        {
                            Success = false,
                            Response = "WhatsApp template name is missing."
                        };
                    }
                    else
                    {
                        // Existing BhashWhatsAppService expects int adminId.
                        // Passing 0 selects the configured Default account.
                        int bhashAccountAdminId = 0;

                        _logger.LogInformation(
                            "WhatsApp template {TemplateName}: TemplateType={TemplateType}, SendType={SendType}, EndpointType={EndpointType}, MessageType={MessageType}",
                            templateName,
                            job.TemplateType,
                            job.TemplateSendType,
                            utilMark == 1 ? "Utility" : "Marketing",
                            templateType);

                        whatsappResult =
                            await _whatsAppService.SendWhatsAppAsync(
                                bhashAccountAdminId,
                                recipientName,
                                recipientMobile,
                                templateName,
                                parameters,
                                utilMark,
                                templateType,
                                mediaUrl,
                                ct);
                    }
                }

                // 11. Send Email
                if (sendEmail)
                {
                    if (string.IsNullOrWhiteSpace(recipientEmail))
                    {
                        emailResult = new DeliveryResult
                        {
                            Success = false,
                            Response = "Recipient email is missing."
                        };
                    }
                    else
                    {
                        string personalizedMessage =
                            BuildMessage(job, recipient);

                        emailResult =
                            await _emailService.SendAsync(
                                recipientName,
                                recipientEmail,
                                personalizedMessage,
                                mediaUrl,
                                generatedImage,
                                ct);
                    }
                }

                // 12. Determine overall delivery status
                bool overallSuccess =
                    (!sendWhatsApp || whatsappResult?.Success == true) &&
                    (!sendEmail || emailResult?.Success == true);

                var errors = new List<string>();

                if (whatsappResult is { Success: false })
                {
                    errors.Add(
                        "WhatsApp: " + whatsappResult.Response);
                }

                if (emailResult is { Success: false })
                {
                    errors.Add(
                        "Email: " + emailResult.Response);
                }

                string deliveryError = string.Join(" | ", errors);

                // 13. Log delivery result
                _logger.LogInformation(
                    "Recipient {RecipientId}, Job {JobId}. WhatsApp={WhatsApp}, Email={Email}",
                    recipient.RecipientId,
                    jobId,
                    whatsappResult?.Success,
                    emailResult?.Success);

                // 14. Update recipient status
                await _dal.UpdateRecipientAsync(
                    jobId,
                    recipient.RecipientId,
                    overallSuccess ? "Success" : "Failed",
                    generatedImage,
                    overallSuccess ? null : deliveryError,
                    ct);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed processing recipient {RecipientId} for job {JobId}",
                    recipient.RecipientId,
                    jobId);

                await _dal.UpdateRecipientAsync(
                    jobId,
                    recipient.RecipientId,
                    "Failed",
                    generatedImage,
                    ex.Message,
                    ct);
            }

            // 15. Update job counters after each recipient
            await _dal.UpdateJobCountersAsync(jobId, ct);
        }

        // 16. Final counter update
        await _dal.UpdateJobCountersAsync(jobId, ct);

        // 17. Complete job
        await _dal.CompleteJobAsync(jobId, ct);

        _logger.LogInformation(
            "Bulk job {JobId} processing completed.",
            jobId);
    }

    private static string BuildMessage(
        BulkJobDetails job,
        BulkRecipientDetails recipient)
    {
        string message = job.TemplateMessage ?? "Hello {{1}}";

        message = message.Replace(
            "{{1}}",
            recipient.Text1 ?? recipient.FullName ?? "");

        message = message.Replace(
            "{{2}}",
            recipient.Text2 ?? "");

        message = message.Replace(
            "{{3}}",
            recipient.Text3 ?? "");

        message = message.Replace(
            "{{4}}",
            recipient.Text4 ?? "");

        message = message.Replace(
            "{{Name}}",
            recipient.FullName ?? "");

        return message;
    }
}
