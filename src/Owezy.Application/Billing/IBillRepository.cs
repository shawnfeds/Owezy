using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public interface IBillRepository
{
    Task<Bill?> GetByIdAsync(BillId id, CancellationToken cancellationToken = default);
    Task AddAsync(Bill bill, CancellationToken cancellationToken = default);
    Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default);
}
