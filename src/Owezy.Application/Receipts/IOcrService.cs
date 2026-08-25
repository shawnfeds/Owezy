using Owezy.Domain.Receipts;

namespace Owezy.Application.Receipts;

/// <summary>
/// OCR engine abstraction. Infrastructure provides the implementation.
/// Returns a raw OcrReceiptDraft; the Application layer applies the qty × price normalisation rule.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Processes an image stream and returns extracted OCR data.
    /// The returned draft may have null fields — OCR is not guaranteed to detect all receipt data.
    /// </summary>
    Task<OcrReceiptDraft> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
