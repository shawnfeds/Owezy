using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;

namespace Owezy.Application.Receipts;

public interface IReceiptService
{
    /// <summary>
    /// Validates the image, stores it, runs OCR synchronously, and returns the OCR draft.
    /// Only the authenticated splitter can upload for their own bill.
    /// OCR draft does NOT modify billing data.
    /// </summary>
    Task<UploadReceiptResult> UploadReceiptAsync(
        PhoneNumber callerPhoneNumber,
        BillId billId,
        Stream imageStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the stored OCR draft for a given receipt.
    /// Only the authenticated splitter can retrieve for their own bill.
    /// </summary>
    Task<ReceiptDraftResult?> GetReceiptDraftAsync(
        PhoneNumber callerPhoneNumber,
        BillId billId,
        ReceiptId receiptId,
        CancellationToken cancellationToken = default);
}
