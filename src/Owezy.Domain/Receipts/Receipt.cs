using Owezy.Domain.Billing;

namespace Owezy.Domain.Receipts;

/// <summary>
/// Receipt aggregate. Represents a receipt image uploaded for a bill and its OCR result.
/// Completely isolated from Bill/BillItems — OCR never mutates billing data.
/// </summary>
public sealed class Receipt
{
    public ReceiptId Id { get; }
    public BillId BillId { get; }
    public string StorageKey { get; }
    public ReceiptStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public OcrReceiptDraft? OcrDraft { get; private set; }

    private Receipt(
        ReceiptId id,
        BillId billId,
        string storageKey,
        ReceiptStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? confirmedAt,
        OcrReceiptDraft? ocrDraft)
    {
        Id = id;
        BillId = billId;
        StorageKey = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
        Status = status;
        CreatedAt = createdAt;
        ConfirmedAt = confirmedAt;
        OcrDraft = ocrDraft;
    }

    public static Receipt Create(BillId billId, string storageKey, DateTimeOffset createdAt)
    {
        if (billId.Value == Guid.Empty)
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("StorageKey cannot be empty.", nameof(storageKey));

        return new Receipt(ReceiptId.New(), billId, storageKey, ReceiptStatus.Created, createdAt, null, null);
    }

    public static Receipt Reconstitute(
        ReceiptId id,
        BillId billId,
        string storageKey,
        ReceiptStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? confirmedAt,
        OcrReceiptDraft? ocrDraft)
    {
        return new Receipt(id, billId, storageKey, status, createdAt, confirmedAt, ocrDraft);
    }

    public void MarkProcessed(OcrReceiptDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        OcrDraft = draft;
        Status = ReceiptStatus.Processed;
    }

    public void MarkFailed()
    {
        Status = ReceiptStatus.Failed;
    }

    public void UpdateDraft(OcrReceiptDraft updatedDraft)
    {
        ArgumentNullException.ThrowIfNull(updatedDraft);

        if (Status == ReceiptStatus.Confirmed)
        {
            throw new InvalidOperationException("Cannot update a receipt draft that has already been confirmed.");
        }

        if (Status != ReceiptStatus.Processed)
        {
            throw new InvalidOperationException("Only processed receipt drafts can be updated.");
        }

        OcrDraft = updatedDraft;
    }

    public void Confirm(DateTimeOffset now)
    {
        if (Status == ReceiptStatus.Confirmed)
        {
            throw new InvalidOperationException("Receipt draft is already confirmed.");
        }

        if (Status != ReceiptStatus.Processed)
        {
            throw new InvalidOperationException("Only processed receipt drafts can be confirmed.");
        }

        Status = ReceiptStatus.Confirmed;
        ConfirmedAt = now;
    }
}
