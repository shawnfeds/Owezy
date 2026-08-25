using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Owezy.Application.Receipts;
using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;

namespace Owezy.Infrastructure.Persistence;

public sealed class SqlReceiptRepository : IReceiptRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly OwezyDbContext _context;

    public SqlReceiptRepository(OwezyDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(Receipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var row = MapToRow(receipt);
        await _context.Receipts.AddAsync(row, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Receipt?> GetByIdAsync(ReceiptId receiptId, CancellationToken cancellationToken = default)
    {
        if (receiptId.Value == Guid.Empty)
        {
            return null;
        }

        var row = await _context.Receipts
            .FirstOrDefaultAsync(r => r.Id == receiptId.Value, cancellationToken);

        return row is null ? null : MapToDomain(row);
    }

    public async Task UpdateAsync(Receipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var existingRow = await _context.Receipts
            .FirstOrDefaultAsync(r => r.Id == receipt.Id.Value, cancellationToken);

        if (existingRow is null)
        {
            throw new InvalidOperationException($"Receipt with ID '{receipt.Id}' was not found for update.");
        }

        existingRow.Status = (int)receipt.Status;
        existingRow.ConfirmedAt = receipt.ConfirmedAt;
        existingRow.OcrResultJson = receipt.OcrDraft is null
            ? null
            : JsonSerializer.Serialize(receipt.OcrDraft, JsonOpts);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ReceiptRow MapToRow(Receipt receipt)
    {
        return new ReceiptRow
        {
            Id = receipt.Id.Value,
            BillId = receipt.BillId.Value,
            StorageKey = receipt.StorageKey,
            Status = (int)receipt.Status,
            CreatedAt = receipt.CreatedAt,
            ConfirmedAt = receipt.ConfirmedAt,
            OcrResultJson = receipt.OcrDraft is null
                ? null
                : JsonSerializer.Serialize(receipt.OcrDraft, JsonOpts)
        };
    }

    private static Receipt MapToDomain(ReceiptRow row)
    {
        OcrReceiptDraft? ocrDraft = null;
        if (!string.IsNullOrWhiteSpace(row.OcrResultJson))
        {
            try
            {
                ocrDraft = JsonSerializer.Deserialize<OcrReceiptDraft>(row.OcrResultJson, JsonOpts);
            }
            catch
            {
                ocrDraft = null;
            }
        }

        return Receipt.Reconstitute(
            new ReceiptId(row.Id),
            new BillId(row.BillId),
            row.StorageKey,
            (ReceiptStatus)row.Status,
            row.CreatedAt,
            row.ConfirmedAt,
            ocrDraft
        );
    }
}
