namespace Owezy.Domain.Receipts;

/// <summary>
/// Structured OCR output from a receipt scan.
/// All fields are optional — OCR cannot always detect every field.
/// All monetary values are decimal.
/// This is a DRAFT only. It must NOT be used to automatically create BillItems.
/// </summary>
public sealed class OcrReceiptDraft
{
    public string? MerchantName { get; init; }
    public string? ReceiptDate { get; init; }
    public string? Currency { get; init; }
    public decimal? Subtotal { get; init; }
    public decimal? Tax { get; init; }
    public decimal? Discount { get; init; }
    public decimal? Total { get; init; }
    public IReadOnlyList<OcrLineItem> LineItems { get; init; } = Array.Empty<OcrLineItem>();
}
