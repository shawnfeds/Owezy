using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class BillTests
{
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");
    private readonly DateTimeOffset _now = new(2026, 8, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInput_InitializesBillAndAddsSplitterAsFirstParticipant()
    {
        var bill = Bill.Create("Weekend Trip", _splitterPhone, _now);

        Assert.NotNull(bill);
        Assert.NotEqual(Guid.Empty, bill.Id.Value);
        Assert.Equal("Weekend Trip", bill.Title);
        Assert.Equal(_splitterPhone, bill.SplitterPhoneNumber);
        Assert.Equal(_now, bill.CreatedAt);
        Assert.Equal(BillStatus.Active, bill.Status);

        Assert.Single(bill.Participants);
        var initialParticipant = bill.Participants.First();
        Assert.Equal(_splitterPhone, initialParticipant.PhoneNumber);
        Assert.Equal(bill.Id, initialParticipant.BillId);
        Assert.Equal(_now, initialParticipant.JoinedAt);
    }

    [Fact]
    public void Create_NullOrEmptyTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Bill.Create("", _splitterPhone, _now));
        Assert.Throws<ArgumentException>(() => Bill.Create("   ", _splitterPhone, _now));
    }

    [Fact]
    public void AddParticipant_NewPhoneNumber_AddsParticipant()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);
        var joinTime = _now.AddMinutes(10);

        var participant = bill.AddParticipant(_participantPhone, joinTime);

        Assert.NotNull(participant);
        Assert.Equal(_participantPhone, participant.PhoneNumber);
        Assert.Equal(bill.Id, participant.BillId);
        Assert.Equal(2, bill.Participants.Count);
        Assert.Contains(participant, bill.Participants);
    }

    [Fact]
    public void AddParticipant_DuplicatePhoneNumber_ThrowsInvalidOperationException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _now);

        // Attempting to add the splitter again
        Assert.Throws<InvalidOperationException>(() => bill.AddParticipant(_splitterPhone, _now.AddMinutes(5)));

        // Adding a new participant then adding them twice
        bill.AddParticipant(_participantPhone, _now.AddMinutes(5));
        Assert.Throws<InvalidOperationException>(() => bill.AddParticipant(_participantPhone, _now.AddMinutes(10)));
    }
}
