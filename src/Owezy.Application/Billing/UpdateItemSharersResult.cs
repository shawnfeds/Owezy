using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record UpdateItemSharersResult(
    BillItemId ItemId,
    BillId BillId,
    IReadOnlyCollection<ParticipantId> SharerParticipantIds
);
