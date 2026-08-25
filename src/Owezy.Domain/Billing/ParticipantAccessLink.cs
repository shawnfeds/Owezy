namespace Owezy.Domain.Billing;

public sealed class ParticipantAccessLink
{
    public BillId BillId { get; }
    public ParticipantId ParticipantId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public bool IsRevoked { get; private set; }

    private ParticipantAccessLink(
        BillId billId,
        ParticipantId participantId,
        string tokenHash,
        DateTimeOffset createdAt,
        bool isRevoked)
    {
        BillId = billId;
        ParticipantId = participantId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        IsRevoked = isRevoked;
    }

    public static ParticipantAccessLink Create(
        BillId billId,
        ParticipantId participantId,
        string tokenHash,
        DateTimeOffset createdAt)
    {
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }
        if (participantId.Value == Guid.Empty)
        {
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(participantId));
        }
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("TokenHash cannot be null or empty.", nameof(tokenHash));
        }

        return new ParticipantAccessLink(billId, participantId, tokenHash.Trim(), createdAt, false);
    }

    public static ParticipantAccessLink Reconstitute(
        BillId billId,
        ParticipantId participantId,
        string tokenHash,
        DateTimeOffset createdAt,
        bool isRevoked)
    {
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }
        if (participantId.Value == Guid.Empty)
        {
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(participantId));
        }
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("TokenHash cannot be null or empty.", nameof(tokenHash));
        }

        return new ParticipantAccessLink(billId, participantId, tokenHash.Trim(), createdAt, isRevoked);
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
