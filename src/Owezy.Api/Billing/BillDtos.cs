namespace Owezy.Api.Billing;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateBillHttpRequest(string? Title);

public sealed record AddParticipantHttpRequest(string? PhoneNumber);

public sealed record AddBillItemHttpRequest(
    string? Description,
    int Quantity,
    decimal Amount,
    List<string>? SharerParticipantIds
);

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

public sealed record AddBillItemHttpResponse(
    string ItemId,
    string BillId,
    string Description,
    int Quantity,
    decimal Amount,
    List<string> SharerParticipantIds
);
