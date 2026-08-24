using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class BillLifecycleTests
{
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");
    private readonly DateTimeOffset _now = new(2026, 8, 24, 15, 0, 0, TimeSpan.Zero);

    private Bill CreateBillWithItem()
    {
        var bill = Bill.Create("Team Dinner", _splitterPhone, _now);
        var splitterPart = bill.Participants.First();
        bill.AddItem("Pizza", 1, 900.00m, new[] { splitterPart.Id });
        return bill;
    }

    [Fact]
    public void NewBill_StartsAsOpen()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        Assert.True(bill.IsOpen);
        Assert.False(bill.IsFinalized);
        Assert.Equal(BillStatus.Active, bill.Status);
    }

    [Fact]
    public void OpenBill_CanAddParticipant()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var participant = bill.AddParticipant(_participantPhone, _now.AddMinutes(1));
        Assert.NotNull(participant);
        Assert.Equal(2, bill.Participants.Count);
    }

    [Fact]
    public void OpenBill_CanAddItem()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var splitterPart = bill.Participants.First();
        var item = bill.AddItem("Pizza", 2, 600.00m, new[] { splitterPart.Id });
        Assert.NotNull(item);
        Assert.Single(bill.Items);
    }

    [Fact]
    public void OpenBill_WithItem_CanFinalize()
    {
        var bill = CreateBillWithItem();
        bill.Finalize(_now.AddMinutes(5));

        Assert.True(bill.IsFinalized);
        Assert.False(bill.IsOpen);
        Assert.Equal(BillStatus.Finalized, bill.Status);
        Assert.NotNull(bill.FinalizedAt);
    }

    [Fact]
    public void FinalizedBill_FinalizedAtTimestampIsSet()
    {
        var bill = CreateBillWithItem();
        var finalizeTime = _now.AddMinutes(10);
        bill.Finalize(finalizeTime);
        Assert.Equal(finalizeTime, bill.FinalizedAt);
    }

    [Fact]
    public void FinalizedBill_CannotFinalizeAgain()
    {
        var bill = CreateBillWithItem();
        bill.Finalize(_now.AddMinutes(5));

        var ex = Assert.Throws<InvalidOperationException>(() => bill.Finalize(_now.AddMinutes(10)));
        Assert.Contains("already finalized", ex.Message);
    }

    [Fact]
    public void FinalizedBill_CannotAddParticipant()
    {
        var bill = CreateBillWithItem();
        bill.Finalize(_now.AddMinutes(5));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            bill.AddParticipant(_participantPhone, _now.AddMinutes(6)));
        Assert.Contains("finalized", ex.Message);
    }

    [Fact]
    public void FinalizedBill_CannotAddItem()
    {
        var bill = CreateBillWithItem();
        bill.Finalize(_now.AddMinutes(5));
        var splitterPart = bill.Participants.First();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            bill.AddItem("Dessert", 1, 200.00m, new[] { splitterPart.Id }));
        Assert.Contains("finalized", ex.Message);
    }

    [Fact]
    public void Bill_WithNoItems_CannotFinalize()
    {
        var bill = Bill.Create("Empty Bill", _splitterPhone, _now);

        var ex = Assert.Throws<InvalidOperationException>(() => bill.Finalize(_now.AddMinutes(5)));
        Assert.Contains("item", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Bill_WithZeroParticipants_CannotFinalize()
    {
        var billId = BillId.New();
        var sharerId = ParticipantId.New();
        var item = BillItem.Reconstitute(BillItemId.New(), billId, "Pizza", 1, 500.00m, new[] { sharerId });
        var bill = Bill.Reconstitute(
            billId,
            "Reconstituted Bill",
            _splitterPhone,
            _now,
            BillStatus.Active,
            participants: Array.Empty<Participant>(),
            items: new[] { item }
        );

        var ex = Assert.Throws<InvalidOperationException>(() => bill.Finalize(_now.AddMinutes(5)));
        Assert.Contains("participant", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void FinalizedBill_StatusStaysFinalized()
    {
        var bill = CreateBillWithItem();
        bill.Finalize(_now.AddMinutes(5));
        Assert.Equal(BillStatus.Finalized, bill.Status);
        // Cannot revert — no transition back
        Assert.True(bill.IsFinalized);
    }

    [Fact]
    public void Reconstitute_FinalizedBill_RestoresFinalizedState()
    {
        var finalizedAt = _now.AddMinutes(30);
        var bill = Bill.Reconstitute(
            BillId.New(),
            "Old Dinner",
            _splitterPhone,
            _now,
            BillStatus.Finalized,
            new[] { Participant.Create(BillId.New(), _splitterPhone, _now) },
            null,
            finalizedAt
        );

        Assert.True(bill.IsFinalized);
        Assert.Equal(finalizedAt, bill.FinalizedAt);
    }
}
