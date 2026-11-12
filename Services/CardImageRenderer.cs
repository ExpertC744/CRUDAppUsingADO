
using CRUDAppUsingADO.Models.Bulk;
using SkiaSharp;
using System.Text.Json;

namespace CRUDAppUsingADO.Services;

public sealed class CardImageRenderer
{
    private readonly IWebHostEnvironment _environment;

    public CardImageRenderer(IWebHostEnvironment environment)
    {
        _environment = environment;
    }
    private string ResolveImagePath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new FileNotFoundException("Card background image path is empty.");

        // Decode URL-encoded characters and normalize slashes.
        string relativePath = Uri.UnescapeDataString(imagePath.Trim())
            .Replace('\\', '/')
            .TrimStart('/');

        string webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        string[] candidates =
        {
        Path.GetFullPath(Path.Combine(webRoot, relativePath)),
        Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativePath))
    };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            "Card background image not found. " +
            $"Database path: {imagePath}. " +
            $"WebRootPath: {_environment.WebRootPath}. " +
            $"ContentRootPath: {_environment.ContentRootPath}. " +
            $"Checked paths: {string.Join(" | ", candidates)}");
    }


    public async Task<string> GenerateAsync(
        BulkJobDetails job,
        BulkRecipientDetails recipient,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(job.ImagePath))
        {
            throw new InvalidOperationException("Card background image is missing in CardDesigns.");
        }

        // Resolve the actual physical image path.
        string imagePath = ResolveImagePath(job.ImagePath);

        //string relativePath = job.ImagePath
        //    .TrimStart('/', '\\')
        //    .Replace('/', Path.DirectorySeparatorChar);

        string relativePath = Uri.UnescapeDataString(
    job.ImagePath.TrimStart('/', '\\')
).Replace('/', Path.DirectorySeparatorChar);

        string webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        //string imagePath = Path.Combine(webRoot, relativePath);

        if (!File.Exists(imagePath))
            throw new FileNotFoundException(
                "Card background image not found.", imagePath);

        string outputDirectory = Path.Combine(
            webRoot,
            "generated",
            "bulk-jobs",
            job.JobId.ToString("N"));

        Directory.CreateDirectory(outputDirectory);

        string outputFile = Path.Combine(
            outputDirectory,
            $"{recipient.RecipientId}.png");

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

        canvas.DrawBitmap(
            bitmap,
            new SKRect(0, 0, width, height));

        var values = GetValues(job, recipient);

        // Default text positions.
        // These will need adjustment to match your actual invitation design.
        DrawText(
            canvas, values["1"],
            width / 2f, 650,
            52, SKColors.DarkRed, true);

        DrawText(
            canvas, values["2"],
            width / 2f, 730,
            38, SKColors.Black, false);

        DrawText(
            canvas, values["3"],
            width / 2f, 800,
            30, SKColors.DarkSlateGray, false);

        canvas.Flush();

        using var image = surface.Snapshot();

        using var data = image.Encode(
            SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException(
                "Unable to encode generated PNG.");

        byte[] imageBytes = data.ToArray();

        await File.WriteAllBytesAsync(
            outputFile,
            imageBytes,
            ct);

        return "/generated/bulk-jobs/" +
               job.JobId.ToString("N") + "/" +
               recipient.RecipientId + ".png";
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
            if (!string.IsNullOrWhiteSpace(job.ParameterValuesJson))
            {
                var custom =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        job.ParameterValuesJson);

                if (custom != null)
                {
                    foreach (var item in custom)
                        values[item.Key] = item.Value ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore invalid optional parameter JSON.
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
            bold ? SKFontStyle.Bold : SKFontStyle.Normal);

        using var font = new SKFont(typeface, size);

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
