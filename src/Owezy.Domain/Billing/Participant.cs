using Owezy.Domain.Auth;

namespace Owezy.Domain.Billing;

public sealed class Participant
{
    public ParticipantId Id { get; }
    public BillId BillId { get; }
    public PhoneNumber PhoneNumber { get; }
    public DateTimeOffset JoinedAt { get; }

    private Participant(ParticipantId id, BillId billId, PhoneNumber phoneNumber, DateTimeOffset joinedAt)
    {
        Id = id;
        BillId = billId;
        PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
        JoinedAt = joinedAt;
    }

    public static Participant Create(BillId billId, PhoneNumber phoneNumber, DateTimeOffset joinedAt)
    {
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return new Participant(ParticipantId.New(), billId, phoneNumber, joinedAt);
    }

    public static Participant Reconstitute(ParticipantId id, BillId billId, PhoneNumber phoneNumber, DateTimeOffset joinedAt)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(id));
        }
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }

        return new Participant(id, billId, phoneNumber, joinedAt);
    }
}
