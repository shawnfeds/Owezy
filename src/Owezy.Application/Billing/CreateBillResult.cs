using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record CreateBillResult(
    BillId BillId,
    string Title,
    PhoneNumber SplitterPhoneNumber,
    int ParticipantCount,
    DateTimeOffset CreatedAt
);
