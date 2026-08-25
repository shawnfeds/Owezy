namespace Owezy.Domain.Receipts;

/// <summary>
/// A single line item extracted from a receipt by OCR.
/// All monetary fields are decimal to prevent floating-point errors.
/// IsLineTotalDerived is true when LineTotal was computed from Quantity × UnitPrice
/// rather than directly detected by OCR.
/// </summary>
public sealed class OcrLineItem
{
    public string Description { get; init; } = string.Empty;
    public decimal? Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? LineTotal { get; init; }
    public bool IsLineTotalDerived { get; init; }
    public decimal? Confidence { get; init; }
}
