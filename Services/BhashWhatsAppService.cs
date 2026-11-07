namespace CRUDAppUsingADO.Services;

public sealed class BhashWhatsAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BhashWhatsAppService> _logger;

    public BhashWhatsAppService(
        IConfiguration configuration,
        ILogger<BhashWhatsAppService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendImageAsync(
        string mobile,
        string imageUrl,
        string message,
        CancellationToken ct)
    {
        // Do not invent a BhashSMS endpoint or request format.
        // These settings and the request body must match your
        // actual BhashSMS WhatsApp API documentation.

        var endpoint = _configuration["BhashSMS:WhatsAppImageEndpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "BhashSMS WhatsApp image endpoint is not configured. " +
                "Configure the provider endpoint and request payload before enabling delivery.");
        }

        // Intentionally stop here until the actual BhashSMS API
        // request format, authentication method and media URL
        // requirements are confirmed.
        _logger.LogWarning(
            "WhatsApp delivery for {Mobile} is not enabled. " +
            "The BhashSMS request contract has not yet been configured.",
            mobile);

        await Task.CompletedTask;

        throw new NotSupportedException(
            "BhashSMS request implementation is pending provider API details.");
    }
}