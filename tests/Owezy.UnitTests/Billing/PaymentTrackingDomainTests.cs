using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class PaymentTrackingDomainTests
{
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");
    private readonly DateTimeOffset _now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private Bill CreateFinalizedBill(out Participant splitterPart, out Participant secondPart)
    {
        var bill = Bill.Create("Team Dinner", _splitterPhone, _now);
        splitterPart = bill.Participants.First();
        secondPart = bill.AddParticipant(_participantPhone, _now.AddMinutes(1));
        bill.AddItem("Pizza", 1, 600m, new[] { splitterPart.Id, secondPart.Id });
        bill.Finalize(_now.AddMinutes(5));
        return bill;
    }

    [Fact]
    public void NewParticipant_PaymentStateIsUnpaid()
    {
        var participant = Participant.Create(BillId.New(), _participantPhone, _now);
        Assert.Equal(PaymentStatus.Unpaid, participant.PaymentStatus);
        Assert.Null(participant.PaidAt);
    }

    [Fact]
    public void Participant_MarkPaid_TransitionsToPaidWithTimestamp()
    {
        var participant = Participant.Create(BillId.New(), _participantPhone, _now);
        var paidTime = _now.AddMinutes(10);

        participant.MarkPaid(paidTime);

        Assert.Equal(PaymentStatus.Paid, participant.PaymentStatus);
        Assert.Equal(paidTime, participant.PaidAt);
    }

    [Fact]
    public void Participant_MarkPaid_IsIdempotent_DoesNotOverwriteOriginalTimestamp()
    {
        var participant = Participant.Create(BillId.New(), _participantPhone, _now);
        var originalPaidTime = _now.AddMinutes(10);
        participant.MarkPaid(originalPaidTime);

        var secondTime = _now.AddMinutes(30);
        participant.MarkPaid(secondTime);

        Assert.Equal(PaymentStatus.Paid, participant.PaymentStatus);
        Assert.Equal(originalPaidTime, participant.PaidAt);
    }

    [Fact]
    public void FinalizedBill_MarkParticipantPaid_Succeeds()
    {
        var bill = CreateFinalizedBill(out _, out var secondPart);
        var paidTime = _now.AddMinutes(15);

        bill.MarkParticipantPaid(secondPart.Id, paidTime);

        Assert.Equal(PaymentStatus.Paid, secondPart.PaymentStatus);
        Assert.Equal(paidTime, secondPart.PaidAt);
    }

    [Fact]
    public void OpenBill_MarkParticipantPaid_ThrowsInvalidOperationException()
    {
        var bill = Bill.Create("Open Bill", _splitterPhone, _now);
        var splitterPart = bill.Participants.First();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            bill.MarkParticipantPaid(splitterPart.Id, _now));
        Assert.Contains("finalized", ex.Message);
    }

    [Fact]
    public void FinalizedBill_MarkParticipantPaid_ForNonMember_ThrowsArgumentException()
    {
        var bill = CreateFinalizedBill(out _, out _);
        var foreignParticipantId = ParticipantId.New();

        var ex = Assert.Throws<ArgumentException>(() =>
            bill.MarkParticipantPaid(foreignParticipantId, _now));
        Assert.Contains("does not belong to bill", ex.Message);
    }
}
