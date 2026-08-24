using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record CalculateItemSharesRequest(BillId BillId, BillItemId ItemId);

public sealed record CalculateItemSharesResult(
    BillId BillId,
    BillItemId ItemId,
    string ItemDescription,
    decimal TotalAmount,
    IReadOnlyList<ParticipantShareResult> Shares
);
