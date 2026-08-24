using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed record ParticipantShareResult(ParticipantId ParticipantId, decimal Amount);
