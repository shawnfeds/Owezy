using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Application.Receipts;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;
using Xunit;

namespace Owezy.UnitTests.Receipts;

public class ReceiptServiceTests
{
    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();

        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default)
        {
            Store.TryGetValue(id, out var b);
            return Task.FromResult(b);
        }

        public Task<Bill?> GetByAccessLinkHashAsync(string tokenHash, CancellationToken ct = default)
        {
            var bill = Store.Values.FirstOrDefault(b => b.AccessLinks.Any(l => l.TokenHash == tokenHash && !l.IsRevoked));
            return Task.FromResult(bill);
        }

        public Task AddAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
        public Task UpdateAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
    }

    private class InMemoryReceiptRepository : IReceiptRepository
    {
        public Dictionary<ReceiptId, Receipt> Store { get; } = new();

        public Task AddAsync(Receipt receipt, CancellationToken cancellationToken = default)
        {
            Store[receipt.Id] = receipt;
            return Task.CompletedTask;
        }

        public Task<Receipt?> GetByIdAsync(ReceiptId receiptId, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(receiptId, out var r);
            return Task.FromResult(r);
        }

        public Task UpdateAsync(Receipt receipt, CancellationToken cancellationToken = default)
        {
            Store[receipt.Id] = receipt;
            return Task.CompletedTask;
        }
    }

    private class InMemoryReceiptStorage : IReceiptStorage
    {
        public Dictionary<string, byte[]> StoredFiles { get; } = new();

        public Task<string> StoreAsync(Stream imageStream, string fileExtension, CancellationToken cancellationToken = default)
        {
            var key = $"{Guid.NewGuid():N}.{fileExtension}";
            using var ms = new MemoryStream();
            imageStream.CopyTo(ms);
            StoredFiles[key] = ms.ToArray();
            return Task.FromResult(key);
        }
    }

    private class TestOcrService : IOcrService
    {
        public bool ShouldFail { get; set; }
        public OcrReceiptDraft PresetDraft { get; set; } = new()
        {
            MerchantName = "Pizza Hut",
            Total = 500m,
            LineItems = new[]
            {
                new OcrLineItem { Description = "Pizza", Quantity = 2m, UnitPrice = 250m }
            }
        };

        public Task<OcrReceiptDraft> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("OCR processing engine failure.");
            }
            return Task.FromResult(PresetDraft);
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private readonly InMemoryBillRepository _billRepo = new();
    private readonly InMemoryReceiptRepository _receiptRepo = new();
    private readonly InMemoryReceiptStorage _storage = new();
    private readonly TestOcrService _ocr = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _otherPhone = PhoneNumber.Create("+919123456789");

    private ReceiptService CreateService() => new(_billRepo, _receiptRepo, _storage, _ocr, _clock);

    private static MemoryStream CreateValidJpegStream()
    {
        // JPEG magic bytes: FF D8 FF 00 ... plus some bytes
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        return new MemoryStream(bytes);
    }

    [Fact]
    public async Task Splitter_UploadReceipt_Succeeds_GeneratesDraft_DoesNotModifyBill()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        var result = await svc.UploadReceiptAsync(
            _splitterPhone, bill.Id, stream, "receipt.jpg", "image/jpeg", stream.Length);

        Assert.NotNull(result);
        Assert.Equal(bill.Id, result.BillId);
        Assert.Equal(ReceiptStatus.Processed, result.Status);
        Assert.NotNull(result.OcrDraft);
        Assert.Equal("Pizza Hut", result.OcrDraft.MerchantName);
        Assert.Single(result.OcrDraft.LineItems);

        // Verify OCR line item derivation rule was applied
        var lineItem = result.OcrDraft.LineItems[0];
        Assert.Equal(500m, lineItem.LineTotal);
        Assert.True(lineItem.IsLineTotalDerived);

        // Verify BILL IS UNTOUCHED (OCR ISOLATION!)
        var savedBill = await _billRepo.GetByIdAsync(bill.Id);
        Assert.Empty(savedBill!.Items);
    }

    [Fact]
    public async Task NonSplitter_UploadReceipt_ThrowsUnauthorizedAccessException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UploadReceiptAsync(_otherPhone, bill.Id, stream, "receipt.jpg", "image/jpeg", stream.Length));
    }

    [Fact]
    public async Task UploadReceipt_NonExistentBill_ThrowsKeyNotFoundException()
    {
        var svc = CreateService();
        using var stream = CreateValidJpegStream();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UploadReceiptAsync(_splitterPhone, BillId.New(), stream, "receipt.jpg", "image/jpeg", stream.Length));
    }

    [Fact]
    public async Task UploadReceipt_UnsupportedExtension_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadReceiptAsync(_splitterPhone, bill.Id, stream, "receipt.pdf", "image/jpeg", stream.Length));
    }

    [Fact]
    public async Task UploadReceipt_UnsupportedContentType_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadReceiptAsync(_splitterPhone, bill.Id, stream, "receipt.jpg", "application/pdf", stream.Length));
    }

    [Fact]
    public async Task UploadReceipt_InvalidMagicBytes_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        // Text file content pretending to be JPG
        var badBytes = "THIS IS NOT AN IMAGE"u8.ToArray();
        using var stream = new MemoryStream(badBytes);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadReceiptAsync(_splitterPhone, bill.Id, stream, "receipt.jpg", "image/jpeg", stream.Length));
    }

    [Fact]
    public async Task UploadReceipt_OcrEngineFailure_MarksReceiptAsFailed_DoesNotThrow()
    {
        var svc = CreateService();
        _ocr.ShouldFail = true;

        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        var result = await svc.UploadReceiptAsync(
            _splitterPhone, bill.Id, stream, "receipt.jpg", "image/jpeg", stream.Length);

        Assert.NotNull(result);
        Assert.Equal(ReceiptStatus.Failed, result.Status);
        Assert.Null(result.OcrDraft);
    }

    [Fact]
    public async Task Splitter_GetReceiptDraft_ReturnsDraft()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        var uploadResult = await svc.UploadReceiptAsync(
            _splitterPhone, bill.Id, stream, "receipt.jpg", "image/jpeg", stream.Length);

        var draftResult = await svc.GetReceiptDraftAsync(_splitterPhone, bill.Id, uploadResult.ReceiptId);

        Assert.NotNull(draftResult);
        Assert.Equal(uploadResult.ReceiptId, draftResult.ReceiptId);
        Assert.Equal(ReceiptStatus.Processed, draftResult.Status);
        Assert.NotNull(draftResult.OcrDraft);
    }

    [Fact]
    public async Task NonSplitter_GetReceiptDraft_ThrowsUnauthorizedAccessException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill);

        using var stream = CreateValidJpegStream();
        var uploadResult = await svc.UploadReceiptAsync(
            _splitterPhone, bill.Id, stream, "receipt.jpg", "image/jpeg", stream.Length);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetReceiptDraftAsync(_otherPhone, bill.Id, uploadResult.ReceiptId));
    }

    [Fact]
    public async Task CrossBill_GetReceiptDraft_ReturnsNull()
    {
        var svc = CreateService();
        var bill1 = Bill.Create("Bill 1", _splitterPhone, _clock.UtcNow);
        var bill2 = Bill.Create("Bill 2", _splitterPhone, _clock.UtcNow);
        await _billRepo.AddAsync(bill1);
        await _billRepo.AddAsync(bill2);

        using var stream = CreateValidJpegStream();
        var uploadResult = await svc.UploadReceiptAsync(
            _splitterPhone, bill1.Id, stream, "receipt.jpg", "image/jpeg", stream.Length);

        // Attempting to query receipt from bill2
        var draftResult = await svc.GetReceiptDraftAsync(_splitterPhone, bill2.Id, uploadResult.ReceiptId);

        Assert.Null(draftResult);
    }
}
