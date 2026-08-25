namespace Owezy.Api.Receipts;

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
