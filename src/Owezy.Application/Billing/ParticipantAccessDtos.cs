using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record GenerateParticipantAccessLinkRequest(
    BillId BillId,
    ParticipantId ParticipantId
);

public sealed record GenerateParticipantAccessLinkResult(
    string RawToken,
    BillId BillId,
    ParticipantId ParticipantId,
    DateTimeOffset CreatedAt
);

public sealed record ParticipantItemShareDto(
    string Description,
    int Quantity,
    decimal ItemTotalAmount,
    decimal MyShareAmount
);

public sealed record ParticipantBillViewResult(
    BillId BillId,
    string BillTitle,
    decimal BillTotalAmount,
    ParticipantId ParticipantId,
    PhoneNumber ParticipantPhoneNumber,
    decimal TotalAmountOwed,
    IReadOnlyList<ParticipantItemShareDto> Items
);
