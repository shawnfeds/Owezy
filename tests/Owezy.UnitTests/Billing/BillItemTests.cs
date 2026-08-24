using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class BillItemTests
{
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");
    private readonly DateTimeOffset _now = new(2026, 8, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddItem_ValidInput_AddsItemWithCorrectSharers()
    {
        var bill = Bill.Create("Pizza Night", _splitterPhone, _now);
        var participant = bill.AddParticipant(_participantPhone, _now.AddMinutes(5));
        var splitterParticipant = bill.Participants.First(p => p.PhoneNumber == _splitterPhone);

        var item = bill.AddItem("Large Pepperoni Pizza", 2, 850.50m, new[] { splitterParticipant.Id, participant.Id });

        Assert.NotNull(item);
        Assert.NotEqual(Guid.Empty, item.Id.Value);
        Assert.Equal(bill.Id, item.BillId);
        Assert.Equal("Large Pepperoni Pizza", item.Description);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(850.50m, item.Amount);
        Assert.Equal(2, item.SharerParticipantIds.Count);
        Assert.Contains(splitterParticipant.Id, item.SharerParticipantIds);
        Assert.Contains(participant.Id, item.SharerParticipantIds);
    }

    [Fact]
    public void AddItem_EmptyDescription_ThrowsArgumentException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var splitterParticipant = bill.Participants.First();

        Assert.Throws<ArgumentException>(() =>
            bill.AddItem("", 1, 100m, new[] { splitterParticipant.Id }));

        Assert.Throws<ArgumentException>(() =>
            bill.AddItem("   ", 1, 100m, new[] { splitterParticipant.Id }));
    }

    [Fact]
    public void AddItem_ZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var splitterParticipant = bill.Participants.First();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bill.AddItem("Garlic Bread", 0, 150m, new[] { splitterParticipant.Id }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bill.AddItem("Garlic Bread", -1, 150m, new[] { splitterParticipant.Id }));
    }

    [Fact]
    public void AddItem_ZeroOrNegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var splitterParticipant = bill.Participants.First();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bill.AddItem("Garlic Bread", 1, 0m, new[] { splitterParticipant.Id }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bill.AddItem("Garlic Bread", 1, -50m, new[] { splitterParticipant.Id }));
    }

    [Fact]
    public void AddItem_EmptySharers_ThrowsArgumentException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);

        Assert.Throws<ArgumentException>(() =>
            bill.AddItem("Salad", 1, 200m, Array.Empty<ParticipantId>()));
    }

    [Fact]
    public void AddItem_DuplicateSharer_ThrowsArgumentException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var splitterParticipant = bill.Participants.First();

        Assert.Throws<ArgumentException>(() =>
            bill.AddItem("Pasta", 1, 350m, new[] { splitterParticipant.Id, splitterParticipant.Id }));
    }

    [Fact]
    public void AddItem_SharerFromAnotherBill_ThrowsArgumentException()
    {
        var billA = Bill.Create("Bill A", _splitterPhone, _now);
        var billB = Bill.Create("Bill B", PhoneNumber.Create("+918888888888"), _now);

        var participantB = billB.Participants.First();

        // Attempting to use participant from Bill B as a sharer for Bill A
        Assert.Throws<ArgumentException>(() =>
            billA.AddItem("Coke", 1, 50m, new[] { participantB.Id }));
    }

    [Fact]
    public void AddItem_SplitterCanBeSharer_Succeeds()
    {
        var bill = Bill.Create("Lunch", _splitterPhone, _now);
        var splitterParticipant = bill.Participants.First();

        var item = bill.AddItem("Burger", 1, 250m, new[] { splitterParticipant.Id });

        Assert.Single(item.SharerParticipantIds);
        Assert.Contains(splitterParticipant.Id, item.SharerParticipantIds);
    }
}
