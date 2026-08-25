using Owezy.Application.Receipts;
using Owezy.Domain.Receipts;
using Tesseract;

namespace Owezy.Infrastructure.Ocr;

/// <summary>
/// Tesseract-based OCR implementation. Stays in Infrastructure — never referenced by Application or Domain.
/// Requires tessdata language files at the configured path (default: tessdata/ relative to working directory).
/// If tessdata is missing, throws an exception which ReceiptService catches and converts to Failed status.
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    private readonly string _tessdataPath;
    private readonly string _language;

    public TesseractOcrService(string tessdataPath = "tessdata", string language = "eng")
    {
        _tessdataPath = tessdataPath;
        _language = language;
    }

    public Task<OcrReceiptDraft> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        // Tesseract is synchronous; wrap in Task for interface compliance.
        cancellationToken.ThrowIfCancellationRequested();

        using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", string.Empty); // allow all characters

        // Copy stream to byte array (Tesseract works with Pix loaded from bytes)
        byte[] imageBytes;
        if (imageStream.CanSeek)
        {
            imageStream.Seek(0, SeekOrigin.Begin);
            using var ms = new MemoryStream();
            imageStream.CopyTo(ms);
            imageBytes = ms.ToArray();
        }
        else
        {
            using var ms = new MemoryStream();
            imageStream.CopyTo(ms);
            imageBytes = ms.ToArray();
        }

        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = engine.Process(pix);

        var rawText = page.GetText() ?? string.Empty;

        // Parse raw text into structured draft (heuristic, best-effort)
        var draft = ReceiptTextParser.Parse(rawText);
        return Task.FromResult(draft);
    }
}
