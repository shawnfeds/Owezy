using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record AddParticipantResult(
    ParticipantId ParticipantId,
    BillId BillId,
    PhoneNumber PhoneNumber,
    DateTimeOffset JoinedAt
);
