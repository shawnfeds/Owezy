using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;

namespace Owezy.Application.Receipts;

public interface IReceiptRepository
{
    Task AddAsync(Receipt receipt, CancellationToken cancellationToken = default);
    Task<Receipt?> GetByIdAsync(ReceiptId receiptId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Receipt receipt, CancellationToken cancellationToken = default);
}
