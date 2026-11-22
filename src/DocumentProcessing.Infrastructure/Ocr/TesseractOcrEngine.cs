using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace DocumentProcessing.Infrastructure.Ocr;

/// <summary>
/// Configuration for Tesseract OCR engine.
/// </summary>
public class TesseractOptions
{
    /// <summary>
    /// Path to the tessdata directory containing trained language data files.
    /// </summary>
    public string TessDataPath { get; set; } = "./tessdata";

    /// <summary>
    /// Language code(s) for OCR. Use '+' for multiple (e.g., "eng+fra").
    /// </summary>
    public string Language { get; set; } = "eng";
}

/// <summary>
/// OCR engine using Tesseract for scanned/image-based documents.
/// Image normalization (orientation, grayscale, contrast) happens earlier in DocumentPreProcessor.
/// </summary>
public class TesseractOcrEngine(
    IOptions<TesseractOptions> options,
    ILogger<TesseractOcrEngine> logger) : ITesseractEngine
{
    private readonly TesseractOptions _options = options.Value;
    private readonly string _tessDataPath = Path.IsPathRooted(options.Value.TessDataPath)
        ? options.Value.TessDataPath
        : Path.Combine(AppContext.BaseDirectory, options.Value.TessDataPath);

    /// <summary>
    /// Runs Tesseract OCR on a pre-processed image stream.
    /// </summary>
    public async Task<TesseractPageResult> RecognizeAsync(Stream imageStream, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            using var buffer = new MemoryStream();
            await imageStream.CopyToAsync(buffer, ct);

            var text = RunTesseract(buffer.ToArray());

            stopwatch.Stop();
            logger.LogDebug("Tesseract OCR completed in {ElapsedMs}ms, extracted {CharCount} chars",
                stopwatch.ElapsedMilliseconds, text.Length);

            return new TesseractPageResult
            {
                Text = text,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Tesseract OCR failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return new TesseractPageResult
            {
                Text = string.Empty,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Runs Tesseract OCR on a PDF page rendered as an image byte array.
    /// </summary>
    public TesseractPageResult Recognize(byte[] imageBytes)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var text = RunTesseract(imageBytes);

            stopwatch.Stop();
            return new TesseractPageResult
            {
                Text = text,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Tesseract OCR failed on image bytes");

            return new TesseractPageResult
            {
                Text = string.Empty,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private string RunTesseract(byte[] imageBytes)
    {
        using var engine = new TesseractEngine(_tessDataPath, _options.Language, EngineMode.Default);
        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = engine.Process(pix);

        return page.GetText().Trim();
    }
}

/// <summary>
/// Result from Tesseract OCR processing a single page/image.
/// </summary>
public class TesseractPageResult
{
    public string Text { get; set; } = string.Empty;
    public long ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}
