using QRCoder;

namespace CRUDAppUsingADO.Services;

public sealed class AttendanceQrService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AttendanceQrService(
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<string> GenerateAsync(
        int eventId,
        CancellationToken ct = default)
    {
        string pageUrl =
            _configuration["Frontend:AttendancePageUrl"]
            ?? throw new InvalidOperationException(
                "Frontend:AttendancePageUrl is missing.");

        string attendanceUrl =
            $"{pageUrl}?eventId={eventId}";

        string root = _environment.WebRootPath
            ?? Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");

        string folder = Path.Combine(
            root,
            "QrAttendence");

        Directory.CreateDirectory(folder);

        string fileName = $"event-{eventId}.png";

        string filePath = Path.Combine(
            folder,
            fileName);

        using var generator = new QRCodeGenerator();

        using var qrData = generator.CreateQrCode(
            attendanceUrl,
            QRCodeGenerator.ECCLevel.Q);

        var qrCode = new PngByteQRCode(qrData);

        byte[] pngBytes = qrCode.GetGraphic(20);

        await File.WriteAllBytesAsync(
            filePath,
            pngBytes,
            ct);

        return $"/QrAttendence/{fileName}";
    }
}