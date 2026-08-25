using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;

namespace Owezy.Application.Receipts;

public sealed class ReceiptService : IReceiptService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    // Allowed content types → canonical extension
    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/jpg"]  = "jpg",
        ["image/png"]  = "png"
    };

    // Allowed extensions (lower-case)
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    // Magic bytes: JPEG = FF D8 FF, PNG = 89 50 4E 47
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagic  = { 0x89, 0x50, 0x4E, 0x47 };

    private readonly IBillRepository _billRepository;
    private readonly IReceiptRepository _receiptRepository;
    private readonly IReceiptStorage _receiptStorage;
    private readonly IOcrService _ocrService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReceiptService(
        IBillRepository billRepository,
        IReceiptRepository receiptRepository,
        IReceiptStorage receiptStorage,
        IOcrService ocrService,
        IDateTimeProvider dateTimeProvider)
    {
        _billRepository    = billRepository    ?? throw new ArgumentNullException(nameof(billRepository));
        _receiptRepository = receiptRepository ?? throw new ArgumentNullException(nameof(receiptRepository));
        _receiptStorage    = receiptStorage    ?? throw new ArgumentNullException(nameof(receiptStorage));
        _ocrService        = ocrService        ?? throw new ArgumentNullException(nameof(ocrService));
        _dateTimeProvider  = dateTimeProvider  ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    public async Task<UploadReceiptResult> UploadReceiptAsync(
        PhoneNumber callerPhoneNumber,
        BillId billId,
        Stream imageStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        ArgumentNullException.ThrowIfNull(imageStream);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));

        // 1. Validate file size
        if (fileSizeBytes > MaxFileSizeBytes)
            throw new InvalidOperationException($"Receipt image exceeds the maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB.");

        if (fileSizeBytes == 0)
            throw new InvalidOperationException("Receipt image cannot be empty.");

        // 2. Validate extension (do not trust client filename, but use it for extension detection)
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Unsupported file type '{extension}'. Only JPEG and PNG images are accepted.");

        // 3. Validate content type
        if (!AllowedContentTypes.TryGetValue(contentType.Split(';')[0].Trim(), out var canonicalExtension))
            throw new InvalidOperationException($"Unsupported content type '{contentType}'. Only JPEG and PNG images are accepted.");

        // 4. Validate magic bytes (read first 4 bytes)
        var header = new byte[4];
        var headerRead = await imageStream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
        if (!IsValidImageHeader(header, headerRead))
            throw new InvalidOperationException("The uploaded file does not appear to be a valid image.");

        // Reset stream to beginning for storage and OCR
        imageStream.Seek(0, SeekOrigin.Begin);

        // 5. Verify bill exists and caller is the splitter
        var bill = await _billRepository.GetByIdAsync(billId, cancellationToken);
        if (bill is null)
            throw new KeyNotFoundException($"Bill with ID '{billId}' was not found.");
        if (bill.SplitterPhoneNumber != callerPhoneNumber)
            throw new UnauthorizedAccessException("Only the bill splitter can upload receipts for this bill.");

        // 6. Store image (server-generated key, never use client filename)
        var storageKey = await _receiptStorage.StoreAsync(imageStream, canonicalExtension, cancellationToken);

        // 7. Create receipt record and persist
        var now = _dateTimeProvider.UtcNow;
        var receipt = Receipt.Create(billId, storageKey, now);
        await _receiptRepository.AddAsync(receipt, cancellationToken);

        // 8. Run OCR synchronously — reset stream first
        imageStream.Seek(0, SeekOrigin.Begin);
        OcrReceiptDraft? finalDraft = null;

        try
        {
            var rawDraft = await _ocrService.ProcessAsync(imageStream, cancellationToken);
            // Apply quantity × unit price normalisation rule in Application layer
            finalDraft = OcrDraftNormalizer.Normalize(rawDraft);
            receipt.MarkProcessed(finalDraft);
        }
        catch (Exception)
        {
            // OCR failure is non-fatal: receipt is saved with Failed status
            receipt.MarkFailed();
        }

        // 9. Persist final state
        await _receiptRepository.UpdateAsync(receipt, cancellationToken);

        return new UploadReceiptResult(
            receipt.Id,
            receipt.BillId,
            receipt.Status,
            receipt.CreatedAt,
            receipt.OcrDraft
        );
    }

    public async Task<ReceiptDraftResult?> GetReceiptDraftAsync(
        PhoneNumber callerPhoneNumber,
        BillId billId,
        ReceiptId receiptId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);

        var receipt = await _receiptRepository.GetByIdAsync(receiptId, cancellationToken);
        if (receipt is null)
            return null;

        // ReceiptId must match the BillId — prevents cross-bill access
        if (receipt.BillId != billId)
            return null;

        // Verify bill ownership
        var bill = await _billRepository.GetByIdAsync(billId, cancellationToken);
        if (bill is null)
            return null;
        if (bill.SplitterPhoneNumber != callerPhoneNumber)
            throw new UnauthorizedAccessException("Only the bill splitter can retrieve receipt drafts.");

        return new ReceiptDraftResult(
            receipt.Id,
            receipt.BillId,
            receipt.Status,
            receipt.CreatedAt,
            receipt.OcrDraft
        );
    }

    private static bool IsValidImageHeader(byte[] header, int bytesRead)
    {
        if (bytesRead < 3) return false;

        // JPEG: FF D8 FF
        if (header[0] == JpegMagic[0] && header[1] == JpegMagic[1] && header[2] == JpegMagic[2])
            return true;

        // PNG: 89 50 4E 47
        if (bytesRead >= 4 &&
            header[0] == PngMagic[0] && header[1] == PngMagic[1] &&
            header[2] == PngMagic[2] && header[3] == PngMagic[3])
            return true;

        return false;
    }
}
