using System.Text.RegularExpressions;
using Owezy.Domain.Receipts;

namespace Owezy.Infrastructure.Ocr;

/// <summary>
/// Heuristic receipt text parser. Converts raw Tesseract text output into a structured OcrReceiptDraft.
/// This is best-effort — receipts vary widely. All fields are nullable.
/// The Application layer's OcrDraftNormalizer applies the qty × price rule afterward.
/// </summary>
internal static class ReceiptTextParser
{
    // Patterns for monetary amounts (supports Indian ₹, $, £, €, plain numbers)
    private static readonly Regex MoneyPattern =
        new(@"[₹$£€]?\s*(\d{1,6}(?:[,\.\s]\d{2,3})*(?:\.\d{2})?)", RegexOptions.Compiled);

    // Typical "Total" line patterns
    private static readonly Regex TotalPattern =
        new(@"(?:grand\s*)?total[:\s]+[₹$£€]?\s*(\d[\d\.,\s]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubtotalPattern =
        new(@"sub\s*total[:\s]+[₹$£€]?\s*(\d[\d\.,\s]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TaxPattern =
        new(@"(?:gst|tax|vat|cgst|sgst)[:\s]+[₹$£€]?\s*(\d[\d\.,\s]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DiscountPattern =
        new(@"discount[:\s]+[₹$£€]?\s*(\d[\d\.,\s]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Date patterns
    private static readonly Regex DatePattern =
        new(@"\b(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2})\b", RegexOptions.Compiled);

    // Line item pattern: "Description  Qty  UnitPrice  LineTotal" (various formats)
    // e.g. "Burger  2  150.00  300.00" or "Burger  300.00" or "2x Burger 300"
    private static readonly Regex LineItemPattern =
        new(@"^(.+?)\s+(\d+(?:\.\d+)?)\s*[xX×]\s*(\d+(?:\.\d+)?)\s+(\d+(?:\.\d+)?)$", RegexOptions.Compiled);

    private static readonly Regex LineItemSimplePattern =
        new(@"^(.+?)\s+(\d+(?:\.\d+)?)$", RegexOptions.Compiled);

    public static OcrReceiptDraft Parse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new OcrReceiptDraft { LineItems = Array.Empty<OcrLineItem>() };
        }

        var lines = rawText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        string? merchantName = lines.Length > 0 ? lines[0].Trim() : null;
        string? receiptDate = ExtractFirstMatch(rawText, DatePattern);
        decimal? total = ExtractDecimal(TotalPattern, rawText);
        decimal? subtotal = ExtractDecimal(SubtotalPattern, rawText);
        decimal? tax = ExtractDecimal(TaxPattern, rawText);
        decimal? discount = ExtractDecimal(DiscountPattern, rawText);

        var lineItems = ParseLineItems(lines);

        return new OcrReceiptDraft
        {
            MerchantName = merchantName,
            ReceiptDate = receiptDate,
            Currency = DetectCurrency(rawText),
            Subtotal = subtotal,
            Tax = tax,
            Discount = discount,
            Total = total,
            LineItems = lineItems
        };
    }

    private static List<OcrLineItem> ParseLineItems(string[] lines)
    {
        var items = new List<OcrLineItem>();

        foreach (var line in lines)
        {
            // Skip header/footer lines
            if (IsHeaderOrFooterLine(line)) continue;

            // Try full pattern: Description Qty x UnitPrice LineTotal
            var fullMatch = LineItemPattern.Match(line);
            if (fullMatch.Success)
            {
                items.Add(new OcrLineItem
                {
                    Description = fullMatch.Groups[1].Value.Trim(),
                    Quantity = ParseDecimal(fullMatch.Groups[2].Value),
                    UnitPrice = ParseDecimal(fullMatch.Groups[3].Value),
                    LineTotal = ParseDecimal(fullMatch.Groups[4].Value),
                    IsLineTotalDerived = false
                });
                continue;
            }

            // Try simple pattern: Description Amount
            var simpleMatch = LineItemSimplePattern.Match(line);
            if (simpleMatch.Success)
            {
                var desc = simpleMatch.Groups[1].Value.Trim();
                var amount = ParseDecimal(simpleMatch.Groups[2].Value);

                // Skip if description looks like a total/tax line
                if (IsHeaderOrFooterLine(desc)) continue;

                items.Add(new OcrLineItem
                {
                    Description = desc,
                    LineTotal = amount,
                    IsLineTotalDerived = false
                });
            }
        }

        return items;
    }

    private static bool IsHeaderOrFooterLine(string line) =>
        Regex.IsMatch(line, @"\b(total|subtotal|tax|gst|vat|discount|thank|receipt|invoice|date|time|bill|order)\b",
            RegexOptions.IgnoreCase);

    private static string? ExtractFirstMatch(string text, Regex pattern)
    {
        var m = pattern.Match(text);
        return m.Success ? m.Value.Trim() : null;
    }

    private static decimal? ExtractDecimal(Regex pattern, string text)
    {
        var m = pattern.Match(text);
        if (!m.Success) return null;
        return ParseDecimal(m.Groups[1].Value);
    }

    private static decimal? ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Replace(",", "").Replace(" ", "").Trim();
        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static string? DetectCurrency(string text)
    {
        if (text.Contains('₹')) return "INR";
        if (text.Contains('$')) return "USD";
        if (text.Contains('£')) return "GBP";
        if (text.Contains('€')) return "EUR";
        return null;
    }
}
