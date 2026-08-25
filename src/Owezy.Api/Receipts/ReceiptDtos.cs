namespace Owezy.Api.Receipts;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record OcrLineItemHttpRequest(
    string? Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? LineTotal,
    decimal? Confidence
);

public sealed record UpdateReceiptDraftHttpRequest(
    string? MerchantName,
    string? ReceiptDate,
    string? Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Discount,
    decimal? Total,
    List<OcrLineItemHttpRequest>? LineItems
);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record OcrLineItemHttpResponse(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? LineTotal,
    bool IsLineTotalDerived,
    decimal? Confidence
);

public sealed record OcrReceiptDraftHttpResponse(
    string? MerchantName,
    string? ReceiptDate,
    string? Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Discount,
    decimal? Total,
    List<OcrLineItemHttpResponse> LineItems
);

public sealed record UploadReceiptHttpResponse(
    string ReceiptId,
    string BillId,
    string Status,
    DateTimeOffset CreatedAt,
    OcrReceiptDraftHttpResponse? OcrDraft
);

public sealed record ReceiptDraftHttpResponse(
    string ReceiptId,
    string BillId,
    string Status,
    DateTimeOffset CreatedAt,
    OcrReceiptDraftHttpResponse? OcrDraft
);

public sealed record ConfirmReceiptHttpResponse(
    string ReceiptId,
    string BillId,
    DateTimeOffset ConfirmedAt,
    List<string> CreatedItemIds
);
