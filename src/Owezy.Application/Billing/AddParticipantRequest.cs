using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record AddParticipantRequest(BillId BillId, PhoneNumber PhoneNumber);
