using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;

namespace Owezy.Application.Receipts;

public sealed record UploadReceiptResult(
    ReceiptId ReceiptId,
    BillId BillId,
    ReceiptStatus Status,
    DateTimeOffset CreatedAt,
    OcrReceiptDraft? OcrDraft
);

public sealed record ReceiptDraftResult(
    ReceiptId ReceiptId,
    BillId BillId,
    ReceiptStatus Status,
    DateTimeOffset CreatedAt,
    OcrReceiptDraft? OcrDraft
);
