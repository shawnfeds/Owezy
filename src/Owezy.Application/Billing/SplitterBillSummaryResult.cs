using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record SplitterBillSummaryItemDto(
    BillItemId ItemId,
    string Description,
    decimal Quantity,
    decimal Amount,
    IReadOnlyList<ParticipantId> SharerParticipantIds,
    IReadOnlyList<ParticipantShareResult> CalculatedShares
);

public sealed record SplitterBillSummaryParticipantDto(
    ParticipantId ParticipantId,
    PhoneNumber PhoneNumber,
    decimal AmountOwed,
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidAt
);

public sealed record SplitterBillSummaryResult(
    BillId BillId,
    string Title,
    PhoneNumber SplitterPhoneNumber,
    BillStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinalizedAt,
    decimal TotalAmount,
    IReadOnlyList<SplitterBillSummaryParticipantDto> Participants,
    IReadOnlyList<SplitterBillSummaryItemDto> Items
);
