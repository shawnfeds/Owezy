using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record UpdateItemSharersRequest(
    BillId BillId,
    BillItemId ItemId,
    IReadOnlyCollection<ParticipantId> SharerParticipantIds
);
