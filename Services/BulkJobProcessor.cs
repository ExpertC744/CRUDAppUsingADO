using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models.Bulk;

namespace CRUDAppUsingADO.Services;

public sealed class BulkJobProcessor
{
    private readonly BulkCardDAL _dal;
    private readonly CardImageRenderer _renderer;
    private readonly BhashWhatsAppService _whatsApp;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BulkJobProcessor> _logger;

    public BulkJobProcessor(
        BulkCardDAL dal,
        CardImageRenderer renderer,
        BhashWhatsAppService whatsApp,
        IConfiguration configuration,
        ILogger<BulkJobProcessor> logger)
    {
        _dal = dal;
        _renderer = renderer;
        _whatsApp = whatsApp;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _dal.LoadJobAsync(jobId, ct);

        if (job == null)
            throw new InvalidOperationException("Bulk job not found.");

        var recipients = await _dal.LoadJobRecipientsAsync(jobId, ct);

        foreach (var recipient in recipients)
        {
            ct.ThrowIfCancellationRequested();

            string? generatedImage = null;

            try
            {
                await _dal.UpdateRecipientAsync(
                    jobId,
                    recipient.RecipientId,
                    "Processing",
                    null,
                    null,
                    ct);

                if (!string.Equals(
                    job.OutputType, "Image",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException(
                        $"Output type '{job.OutputType}' is not implemented yet.");
                }

                generatedImage = await _renderer.GenerateAsync(
                    job, recipient, ct);

                if (job.Channels.Contains(
                    "WhatsApp",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(recipient.Mobile))
                        throw new InvalidOperationException(
                            "Recipient mobile number is missing.");

                    var publicBaseUrl =
                        _configuration["Frontend:PublicUrl"];

                    if (string.IsNullOrWhiteSpace(publicBaseUrl))
                        throw new InvalidOperationException(
                            "Frontend:PublicUrl is missing from configuration.");

                    string imageUrl =
                        publicBaseUrl.TrimEnd('/') + generatedImage;

                    string message = BuildMessage(job, recipient);

                    await _whatsApp.SendImageAsync(
                        recipient.Mobile,
                        imageUrl,
                        message,
                        ct);
                }
                else
                {
                    throw new NotSupportedException(
                        $"Channel '{job.Channels}' is not implemented.");
                }

                await _dal.UpdateRecipientAsync(
                    jobId,
                    recipient.RecipientId,
                    "Success",
                    generatedImage,
                    null,
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
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

            await _dal.UpdateJobCountersAsync(jobId, ct);
        }

        await _dal.UpdateJobCountersAsync(jobId, ct);
        await _dal.CompleteJobAsync(jobId, ct);
    }

    private static string BuildMessage(
        BulkJobDetails job,
        BulkRecipientDetails recipient)
    {
        string message = job.TemplateMessage ?? "Hello {{1}}";

        message = message.Replace(
            "{{1}}", recipient.Text1 ?? recipient.FullName);

        message = message.Replace(
            "{{2}}", recipient.Text2 ?? "");

        message = message.Replace(
            "{{3}}", recipient.Text3 ?? "");

        message = message.Replace(
            "{{4}}", recipient.Text4 ?? "");

        message = message.Replace(
            "{{Name}}", recipient.FullName);

        return message;
    }
}