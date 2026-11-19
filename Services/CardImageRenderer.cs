using CRUDAppUsingADO.Models.Bulk;
using SkiaSharp;
using System.Text.Json;

namespace CRUDAppUsingADO.Services;

public sealed class CardImageRenderer
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CardImageRenderer> _logger;

    public CardImageRenderer(
        IWebHostEnvironment environment,
        ILogger<CardImageRenderer> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    // Resolve the database image path to its actual physical server path.
    private string ResolveImagePath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new FileNotFoundException(
                "Card background image path is empty.");

        string relativePath = Uri.UnescapeDataString(imagePath.Trim())
            .Replace('\\', '/')
            .TrimStart('/');

        if (Path.IsPathRooted(relativePath) ||
            relativePath.Split('/').Any(x => x == ".."))
        {
            throw new InvalidOperationException(
                "Invalid card background image path.");
        }

        string webRoot = _environment.WebRootPath
            ?? Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");

        string[] allowedRoots =
        {
            Path.GetFullPath(webRoot),
            Path.GetFullPath(
                Path.Combine(_environment.ContentRootPath, "uploads"))
        };

        foreach (string root in allowedRoots)
        {
            string candidate = Path.GetFullPath(
                Path.Combine(root, relativePath));

            // Prevent directory traversal.
            string prefix = root.TrimEnd(
                Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
                continue;

            _logger.LogInformation(
                "Checking card image: {Path}", candidate);

            if (File.Exists(candidate))
            {
                _logger.LogInformation(
                    "Card image found: {Path}", candidate);

                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Card background image not found: {imagePath}");
    }

    // File name = user name, e.g. "Rahul_Patil.png".
    // Letters and digits are kept, everything else becomes "_".
    // If the same name already exists in this job folder: "Rahul_Patil_14.png".
    private static string BuildFileName(
        string outputDirectory,
        BulkRecipientDetails recipient)
    {
        var chars = (recipient.FullName ?? "")
            .Trim()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();

        string safe = new string(chars).Trim('_');

        while (safe.Contains("__"))
            safe = safe.Replace("__", "_");

        if (safe.Length > 80)
            safe = safe[..80].Trim('_');

        if (string.IsNullOrWhiteSpace(safe))
            safe = recipient.RecipientId.ToString();

        string fileName = safe + ".png";

        if (File.Exists(Path.Combine(outputDirectory, fileName)))
            fileName = $"{safe}_{recipient.RecipientId}.png";

        return fileName;
    }

    // true when the saved design already contains its own content (every design saved from the
    // Custom+ screen does: the image already has this user's text drawn into it)
    private static bool DesignAlreadyHasText(BulkJobDetails job)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job.CanvasJson))
                return false;

            using var doc = JsonDocument.Parse(job.CanvasJson);

            return doc.RootElement.TryGetProperty("objects", out var objects) &&
                   objects.ValueKind == JsonValueKind.Array &&
                   objects.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<string> GenerateAsync(
        BulkJobDetails job,
        BulkRecipientDetails recipient,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(job.ImagePath))
        {
            throw new InvalidOperationException(
                "Card background image is missing in CardDesigns.");
        }

        // Resolve the physical image path once.
        // ResolveImagePath already verifies that the file exists.
        string imagePath = ResolveImagePath(job.ImagePath);

        _logger.LogInformation(
            "Generating card for JobId={JobId}, RecipientId={RecipientId}, ImagePath={ImagePath}",
            job.JobId,
            recipient.RecipientId,
            imagePath);

        string webRoot = _environment.WebRootPath
            ?? Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");

        // Generated image output directory.
        string outputDirectory = Path.Combine(
            webRoot,
            "generated",
            "bulk-jobs",
            job.JobId.ToString("N"));

        Directory.CreateDirectory(outputDirectory);

        string fileName = BuildFileName(outputDirectory, recipient);

        string outputFile = Path.Combine(outputDirectory, fileName);

        // Load original card background.
        using var bitmap = SKBitmap.Decode(imagePath)
            ?? throw new InvalidOperationException(
                "Unable to decode card background image.");

        int width = job.CanvasWidth > 0
            ? job.CanvasWidth
            : bitmap.Width;

        int height = job.CanvasHeight > 0
            ? job.CanvasHeight
            : bitmap.Height;

        using var surface = SKSurface.Create(
            new SKImageInfo(width, height))
            ?? throw new InvalidOperationException(
                "Unable to create image surface.");

        SKCanvas canvas = surface.Canvas;

        canvas.Clear(SKColors.White);

        // Draw background image.
        canvas.DrawBitmap(
            bitmap,
            new SKRect(0, 0, width, height));

        // The image saved by Custom+ already has this user's text drawn into it,
        // so nothing is drawn on top (that is what showed every text twice).
        // Only a plain background image (for example uploaded through Postman, no design content)
        // gets the default text lines below.
        if (!DesignAlreadyHasText(job))
        {
            var values = GetValues(job, recipient);

            DrawText(canvas, values["1"], width / 2f, 650, 52, SKColors.DarkRed, true);
            DrawText(canvas, values["2"], width / 2f, 730, 38, SKColors.Black, false);
            DrawText(canvas, values["3"], width / 2f, 800, 30, SKColors.DarkSlateGray, false);
        }

        canvas.Flush();

        using var image = surface.Snapshot();

        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100)
            ?? throw new InvalidOperationException(
                "Unable to encode generated PNG.");

        byte[] imageBytes = data.ToArray();

        await File.WriteAllBytesAsync(
            outputFile,
            imageBytes,
            ct);

        _logger.LogInformation(
            "Generated card successfully. JobId={JobId}, RecipientId={RecipientId}, OutputFile={OutputFile}",
            job.JobId,
            recipient.RecipientId,
            outputFile);

        // Return the public relative URL.
        return "/generated/bulk-jobs/" +
               job.JobId.ToString("N") + "/" +
               Uri.EscapeDataString(fileName);
    }

    private static Dictionary<string, string> GetValues(
        BulkJobDetails job,
        BulkRecipientDetails recipient)
    {
        var values = new Dictionary<string, string>
        {
            ["1"] = recipient.Text1 ?? recipient.FullName,
            ["2"] = recipient.Text2 ?? string.Empty,
            ["3"] = recipient.Text3 ?? string.Empty,
            ["4"] = recipient.Text4 ?? string.Empty,
            ["Name"] = recipient.FullName
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(
                job.ParameterValuesJson))
            {
                var custom =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        job.ParameterValuesJson);

                if (custom != null)
                {
                    foreach (var item in custom)
                    {
                        values[item.Key] =
                            item.Value ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Invalid optional parameter JSON.
            // Continue with the recipient values.
        }

        return values;
    }

    private static void DrawText(
        SKCanvas canvas,
        string text,
        float x,
        float y,
        float size,
        SKColor color,
        bool bold)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        using var typeface = SKTypeface.FromFamilyName(
            "Arial",
            bold
                ? SKFontStyle.Bold
                : SKFontStyle.Normal);

        using var font = new SKFont(
            typeface,
            size);

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true
        };

        canvas.DrawText(
            text,
            x,
            y,
            SKTextAlign.Center,
            font,
            paint);
    }
}