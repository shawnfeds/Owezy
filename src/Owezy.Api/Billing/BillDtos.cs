namespace Owezy.Api.Billing;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateBillHttpRequest(string? Title);

public sealed record AddParticipantHttpRequest(string? PhoneNumber);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record CreateBillHttpResponse(
    string BillId,
    string Title,
    string SplitterPhoneNumber,
    int ParticipantCount,
    DateTimeOffset CreatedAt
);

public sealed record AddParticipantHttpResponse(
    string ParticipantId,
    string BillId,
    string PhoneNumber,
    DateTimeOffset JoinedAt
);
