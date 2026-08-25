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

public sealed record OcrLineItemDto(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? LineTotal,
    decimal? Confidence
);

public sealed record UpdateReceiptDraftRequest(
    string? MerchantName,
    string? ReceiptDate,
    string? Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Discount,
    decimal? Total,
    IReadOnlyList<OcrLineItemDto> LineItems
);

public sealed record ConfirmReceiptResult(
    ReceiptId ReceiptId,
    BillId BillId,
    DateTimeOffset ConfirmedAt,
    IReadOnlyList<BillItemId> CreatedItemIds
);
