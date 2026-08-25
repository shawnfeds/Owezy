using Owezy.Domain.Auth;

namespace Owezy.Domain.Billing;

public sealed class Participant
{
    public ParticipantId Id { get; }
    public BillId BillId { get; }
    public PhoneNumber PhoneNumber { get; }
    public DateTimeOffset JoinedAt { get; }
    public PaymentStatus PaymentStatus { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    private Participant(
        ParticipantId id,
        BillId billId,
        PhoneNumber phoneNumber,
        DateTimeOffset joinedAt,
        PaymentStatus paymentStatus = PaymentStatus.Unpaid,
        DateTimeOffset? paidAt = null)
    {
        Id = id;
        BillId = billId;
        PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
        JoinedAt = joinedAt;
        PaymentStatus = paymentStatus;
        PaidAt = paidAt;
    }

    public static Participant Create(BillId billId, PhoneNumber phoneNumber, DateTimeOffset joinedAt)
    {
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return new Participant(ParticipantId.New(), billId, phoneNumber, joinedAt, PaymentStatus.Unpaid, null);
    }

    public static Participant Reconstitute(
        ParticipantId id,
        BillId billId,
        PhoneNumber phoneNumber,
        DateTimeOffset joinedAt,
        PaymentStatus paymentStatus = PaymentStatus.Unpaid,
        DateTimeOffset? paidAt = null)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(id));
        }
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return new Participant(id, billId, phoneNumber, joinedAt, paymentStatus, paidAt);
    }

    public void MarkPaid(DateTimeOffset now)
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            // Idempotent: do not overwrite timestamp if already paid
            return;
        }

        PaymentStatus = PaymentStatus.Paid;
        PaidAt = now;
    }
}
