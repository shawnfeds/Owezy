using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record AddBillItemResult(
    BillItemId ItemId,
    BillId BillId,
    string Description,
    int Quantity,
    decimal Amount,
    IReadOnlyCollection<ParticipantId> SharerParticipantIds
);
