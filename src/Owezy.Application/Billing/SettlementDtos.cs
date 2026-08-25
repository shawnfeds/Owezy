using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record ParticipantSettlementDto(
    ParticipantId ParticipantId,
    PhoneNumber PhoneNumber,
    decimal AmountOwed,
    decimal AmountPaid,
    decimal AmountRemaining,
    PaymentStatus PaymentStatus
);

public sealed record BillSettlementResult(
    BillId BillId,
    string BillTitle,
    decimal BillTotalAmount,
    decimal TotalOwed,
    decimal TotalPaid,
    decimal TotalRemaining,
    int ParticipantCount,
    int PaidCount,
    int UnpaidCount,
    IReadOnlyList<ParticipantSettlementDto> Participants
);
