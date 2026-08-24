using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record FinalizeBillRequest(BillId BillId);

public sealed record FinalizeBillResult(
    BillId BillId,
    string Title,
    BillStatus Status,
    DateTimeOffset? FinalizedAt
);
