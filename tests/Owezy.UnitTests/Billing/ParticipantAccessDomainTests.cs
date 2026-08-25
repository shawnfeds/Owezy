using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class ParticipantAccessDomainTests
{
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _otherPhone = PhoneNumber.Create("+919123456789");
    private readonly DateTimeOffset _now = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    private Bill CreateFinalizedBill(out Participant splitterPart, out Participant secondPart)
    {
        var bill = Bill.Create("Team Lunch", _splitterPhone, _now);
        splitterPart = bill.Participants.First();
        secondPart = bill.AddParticipant(_otherPhone, _now.AddMinutes(1));
        bill.AddItem("Pizza", 1, 500m, new[] { splitterPart.Id, secondPart.Id });
        bill.Finalize(_now.AddMinutes(5));
        return bill;
    }

    [Fact]
    public void FinalizedBill_GenerateAccessLink_Succeeds()
    {
        var bill = CreateFinalizedBill(out _, out var secondPart);
        var hash = "hash-12345";

        var link = bill.GenerateAccessLink(secondPart.Id, hash, _now.AddMinutes(10));

        Assert.NotNull(link);
        Assert.Equal(bill.Id, link.BillId);
        Assert.Equal(secondPart.Id, link.ParticipantId);
        Assert.Equal(hash, link.TokenHash);
        Assert.False(link.IsRevoked);
        Assert.Single(bill.AccessLinks);
    }

    [Fact]
    public void OpenBill_GenerateAccessLink_ThrowsInvalidOperationException()
    {
        var bill = Bill.Create("Open Bill", _splitterPhone, _now);
        var splitterPart = bill.Participants.First();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            bill.GenerateAccessLink(splitterPart.Id, "hash-123", _now));
        Assert.Contains("finalized", ex.Message);
    }

    [Fact]
    public void FinalizedBill_GenerateAccessLink_ForNonMemberParticipant_ThrowsArgumentException()
    {
        var bill = CreateFinalizedBill(out _, out _);
        var foreignParticipantId = ParticipantId.New();

        var ex = Assert.Throws<ArgumentException>(() =>
            bill.GenerateAccessLink(foreignParticipantId, "hash-123", _now));
        Assert.Contains("does not belong to bill", ex.Message);
    }

    [Fact]
    public void FinalizedBill_GenerateAccessLink_RevokesPreviousActiveLinkForSameParticipant()
    {
        var bill = CreateFinalizedBill(out _, out var secondPart);

        var link1 = bill.GenerateAccessLink(secondPart.Id, "hash-first", _now.AddMinutes(10));
        Assert.False(link1.IsRevoked);

        var link2 = bill.GenerateAccessLink(secondPart.Id, "hash-second", _now.AddMinutes(15));
        Assert.False(link2.IsRevoked);

        Assert.True(link1.IsRevoked);
        Assert.Equal(2, bill.AccessLinks.Count);
        Assert.Single(bill.AccessLinks, l => !l.IsRevoked);
    }
}
