namespace Owezy.Infrastructure.Persistence;

public sealed class ReceiptRow
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>
    /// JSON-serialised OcrReceiptDraft. Null if OCR has not yet run or failed.
    /// Stored as nvarchar(max) — image binary is never stored in SQL.
    /// </summary>
    public string? OcrResultJson { get; set; }
}
