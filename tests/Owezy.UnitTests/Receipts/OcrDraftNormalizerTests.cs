using Owezy.Application.Receipts;
using Owezy.Domain.Receipts;
using Xunit;

namespace Owezy.UnitTests.Receipts;

public class OcrDraftNormalizerTests
{
    [Fact]
    public void CaseA_ExistingLineTotal_IsPreserved_NotOverwrittenByQuantityTimesPrice()
    {
        var raw = new OcrReceiptDraft
        {
            LineItems = new[]
            {
                new OcrLineItem
                {
                    Description = "Item A",
                    Quantity = 2m,
                    UnitPrice = 100m,
                    LineTotal = 190m // e.g. discounted line total from receipt
                }
            }
        };

        var normalized = OcrDraftNormalizer.Normalize(raw);

        var item = Assert.Single(normalized.LineItems);
        Assert.Equal(190m, item.LineTotal);
        Assert.False(item.IsLineTotalDerived);
    }

    [Fact]
    public void CaseB_QuantityAndUnitPricePresent_LineTotalMissing_DerivesLineTotal()
    {
        var raw = new OcrReceiptDraft
        {
            LineItems = new[]
            {
                new OcrLineItem
                {
                    Description = "Item B",
                    Quantity = 3m,
                    UnitPrice = 150m,
                    LineTotal = null
                }
            }
        };

        var normalized = OcrDraftNormalizer.Normalize(raw);

        var item = Assert.Single(normalized.LineItems);
        Assert.Equal(450m, item.LineTotal);
        Assert.True(item.IsLineTotalDerived);
    }

    [Fact]
    public void CaseC_CompleteLineAmountOnly_IsPreservedAsLineTotal()
    {
        var raw = new OcrReceiptDraft
        {
            LineItems = new[]
            {
                new OcrLineItem
                {
                    Description = "Item C",
                    Quantity = null,
                    UnitPrice = null,
                    LineTotal = 299.99m
                }
            }
        };

        var normalized = OcrDraftNormalizer.Normalize(raw);

        var item = Assert.Single(normalized.LineItems);
        Assert.Equal(299.99m, item.LineTotal);
        Assert.False(item.IsLineTotalDerived);
    }

    [Fact]
    public void CaseD_AmbiguousPriceInfo_LineTotalRemainsNull_DoesNotInventValues()
    {
        var raw = new OcrReceiptDraft
        {
            LineItems = new[]
            {
                new OcrLineItem
                {
                    Description = "Ambiguous Item",
                    Quantity = 2m,
                    UnitPrice = null,
                    LineTotal = null
                }
            }
        };

        var normalized = OcrDraftNormalizer.Normalize(raw);

        var item = Assert.Single(normalized.LineItems);
        Assert.Null(item.LineTotal);
        Assert.False(item.IsLineTotalDerived);
    }

    [Fact]
    public void Normalize_PreservesTopLevelFields()
    {
        var raw = new OcrReceiptDraft
        {
            MerchantName = "Supermarket",
            ReceiptDate = "2026-08-25",
            Currency = "INR",
            Subtotal = 1000m,
            Tax = 180m,
            Discount = 50m,
            Total = 1130m,
            LineItems = Array.Empty<OcrLineItem>()
        };

        var normalized = OcrDraftNormalizer.Normalize(raw);

        Assert.Equal("Supermarket", normalized.MerchantName);
        Assert.Equal("2026-08-25", normalized.ReceiptDate);
        Assert.Equal("INR", normalized.Currency);
        Assert.Equal(1000m, normalized.Subtotal);
        Assert.Equal(180m, normalized.Tax);
        Assert.Equal(50m, normalized.Discount);
        Assert.Equal(1130m, normalized.Total);
    }
}
