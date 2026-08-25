using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record MarkParticipantPaidResult(
    ParticipantId ParticipantId,
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidAt
);

public sealed record ParticipantPaymentStatusDto(
    ParticipantId ParticipantId,
    PhoneNumber PhoneNumber,
    decimal AmountOwed,
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidAt
);

public sealed record SplitterBillPaymentsResult(
    BillId BillId,
    string BillTitle,
    decimal BillTotalAmount,
    IReadOnlyList<ParticipantPaymentStatusDto> ParticipantPayments
);
