using Owezy.Domain.Receipts;

namespace Owezy.Application.Receipts;

/// <summary>
/// Applies the authoritative quantity × unit price derivation rule to OCR line items.
/// Lives in the Application layer so it is testable without any Infrastructure dependency.
///
/// Rules:
///   A. If LineTotal is detected by OCR → keep as authoritative.
///   B. If Quantity and UnitPrice are detected but LineTotal is missing
///      → derive LineTotal = Quantity × UnitPrice, mark IsLineTotalDerived = true.
///   C. If only a line amount exists → it is already LineTotal.
///   D. Ambiguous / missing data → LineTotal remains null. Do NOT invent values.
/// </summary>
public static class OcrDraftNormalizer
{
    public static OcrReceiptDraft Normalize(OcrReceiptDraft raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var normalizedItems = raw.LineItems
            .Select(NormalizeLineItem)
            .ToList();

        return new OcrReceiptDraft
        {
            MerchantName = raw.MerchantName,
            ReceiptDate = raw.ReceiptDate,
            Currency = raw.Currency,
            Subtotal = raw.Subtotal,
            Tax = raw.Tax,
            Discount = raw.Discount,
            Total = raw.Total,
            LineItems = normalizedItems
        };
    }

    internal static OcrLineItem NormalizeLineItem(OcrLineItem item)
    {
        // Rule A: existing LineTotal is authoritative — do not overwrite.
        if (item.LineTotal.HasValue)
        {
            return new OcrLineItem
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                IsLineTotalDerived = false,
                Confidence = item.Confidence
            };
        }

        // Rule B: derive LineTotal from Quantity × UnitPrice when both are present.
        if (item.Quantity.HasValue && item.UnitPrice.HasValue)
        {
            var derived = item.Quantity.Value * item.UnitPrice.Value;
            return new OcrLineItem
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = derived,
                IsLineTotalDerived = true,
                Confidence = item.Confidence
            };
        }

        // Rules C & D: preserve as-is with null LineTotal.
        return new OcrLineItem
        {
            Description = item.Description,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            LineTotal = null,
            IsLineTotalDerived = false,
            Confidence = item.Confidence
        };
    }
}
