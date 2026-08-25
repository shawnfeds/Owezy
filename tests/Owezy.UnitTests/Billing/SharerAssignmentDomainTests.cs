using System.Security.Cryptography;
using System.Text;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class SharerAssignmentDomainTests
{
    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();

        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default)
        {
            Store.TryGetValue(id, out var b);
            return Task.FromResult(b);
        }

        public Task<Bill?> GetByAccessLinkHashAsync(string tokenHash, CancellationToken ct = default)
        {
            var bill = Store.Values.FirstOrDefault(b => b.AccessLinks.Any(l => l.TokenHash == tokenHash && !l.IsRevoked));
            return Task.FromResult(bill);
        }

        public Task AddAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
        public Task UpdateAsync(Bill bill, CancellationToken ct = default) { Store[bill.Id] = bill; return Task.CompletedTask; }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private class TestParticipantTokenGenerator : IParticipantTokenGenerator
    {
        public string GenerateToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        public string HashToken(string rawToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    private readonly InMemoryBillRepository _billRepo = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly TestParticipantTokenGenerator _tokenGen = new();
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _otherPhone = PhoneNumber.Create("+919123456789");

    private BillService CreateService() => new(_billRepo, _clock, _tokenGen);

    [Fact]
    public async Task Splitter_AssignOneSharer_Succeeds()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;

        // Add item with 0 sharers (e.g. from OCR confirmation)
        var item = bill.AddItem("Burger", 1, 150m, Array.Empty<ParticipantId>());
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { splitterPartId });
        var res = await svc.UpdateItemSharersAsync(_splitterPhone, req);

        Assert.NotNull(res);
        Assert.Single(res.SharerParticipantIds);
        Assert.Contains(splitterPartId, res.SharerParticipantIds);
    }

    [Fact]
    public async Task Splitter_AssignMultipleSharers_Succeeds()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var p2 = bill.AddParticipant(_otherPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;

        var item = bill.AddItem("Pizza", 1, 600m, Array.Empty<ParticipantId>());
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { splitterPartId, p2.Id });
        var res = await svc.UpdateItemSharersAsync(_splitterPhone, req);

        Assert.Equal(2, res.SharerParticipantIds.Count);
        Assert.Contains(splitterPartId, res.SharerParticipantIds);
        Assert.Contains(p2.Id, res.SharerParticipantIds);
    }

    [Fact]
    public async Task ExistingSharers_CanBeReplaced()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var p2 = bill.AddParticipant(_otherPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;

        var item = bill.AddItem("Fries", 1, 100m, new[] { splitterPartId });
        await _billRepo.AddAsync(bill);

        // Replace splitter with p2
        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { p2.Id });
        var res = await svc.UpdateItemSharersAsync(_splitterPhone, req);

        Assert.Single(res.SharerParticipantIds);
        Assert.Contains(p2.Id, res.SharerParticipantIds);
        Assert.DoesNotContain(splitterPartId, res.SharerParticipantIds);
    }

    [Fact]
    public async Task DuplicateSharers_ThrowsArgumentException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;

        var item = bill.AddItem("Salad", 1, 200m, Array.Empty<ParticipantId>());
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { splitterPartId, splitterPartId });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpdateItemSharersAsync(_splitterPhone, req));
    }

    [Fact]
    public async Task CrossBillParticipant_ThrowsArgumentException()
    {
        var svc = CreateService();
        var billA = Bill.Create("Bill A", _splitterPhone, _clock.UtcNow);
        var billB = Bill.Create("Bill B", _otherPhone, _clock.UtcNow);

        var itemA = billA.AddItem("Coke", 1, 50m, Array.Empty<ParticipantId>());
        var partB = billB.Participants.First();

        await _billRepo.AddAsync(billA);
        await _billRepo.AddAsync(billB);

        var req = new UpdateItemSharersRequest(billA.Id, itemA.Id, new[] { partB.Id });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpdateItemSharersAsync(_splitterPhone, req));
    }

    [Fact]
    public async Task UnknownParticipant_ThrowsArgumentException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var item = bill.AddItem("Coke", 1, 50m, Array.Empty<ParticipantId>());
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { ParticipantId.New() });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpdateItemSharersAsync(_splitterPhone, req));
    }

    [Fact]
    public async Task UnknownItem_ThrowsKeyNotFoundException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, BillItemId.New(), new[] { splitterPartId });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UpdateItemSharersAsync(_splitterPhone, req));
    }

    [Fact]
    public async Task NonSplitter_CannotModifySharers_ThrowsUnauthorizedAccessException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;
        var item = bill.AddItem("Pasta", 1, 300m, Array.Empty<ParticipantId>());
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { splitterPartId });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateItemSharersAsync(_otherPhone, req));
    }

    [Fact]
    public async Task FinalizedBill_CannotModifySharers_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        var bill = Bill.Create("Lunch", _splitterPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;
        var item = bill.AddItem("Pasta", 1, 300m, new[] { splitterPartId });
        bill.Finalize(_clock.UtcNow);
        await _billRepo.AddAsync(bill);

        var req = new UpdateItemSharersRequest(bill.Id, item.Id, new[] { splitterPartId });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateItemSharersAsync(_splitterPhone, req));
    }

    [Fact]
    public void Finalize_BillWithZeroSharerItem_ThrowsInvalidOperationException()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        bill.AddItem("Zero Sharer Item", 1, 100m, Array.Empty<ParticipantId>());

        var ex = Assert.Throws<InvalidOperationException>(() => bill.Finalize(_clock.UtcNow));
        Assert.Contains("zero sharers", ex.Message);
    }

    [Fact]
    public void Finalize_BillWithAllItemsHavingSharers_Succeeds()
    {
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;
        bill.AddItem("Item With Sharer", 1, 100m, new[] { splitterPartId });

        bill.Finalize(_clock.UtcNow);

        Assert.True(bill.IsFinalized);
    }

    [Fact]
    public async Task EqualSplitCalculation_WorksAfterSharerAssignment()
    {
        var svc = CreateService();
        var bill = Bill.Create("Dinner", _splitterPhone, _clock.UtcNow);
        var p2 = bill.AddParticipant(_otherPhone, _clock.UtcNow);
        var splitterPartId = bill.Participants.First().Id;

        // Add 0-sharer item
        var item = bill.AddItem("Soup", 1, 300m, Array.Empty<ParticipantId>());
        await _billRepo.AddAsync(bill);

        // Splitter assigns both participants as sharers
        await svc.UpdateItemSharersAsync(_splitterPhone, new UpdateItemSharersRequest(bill.Id, item.Id, new[] { splitterPartId, p2.Id }));

        // Now calculate shares
        var sharesResult = await svc.CalculateItemSharesAsync(_splitterPhone, new CalculateItemSharesRequest(bill.Id, item.Id));

        Assert.NotNull(sharesResult);
        Assert.Equal(300m, sharesResult.TotalAmount);
        Assert.Equal(2, sharesResult.Shares.Count);
        Assert.All(sharesResult.Shares, s => Assert.Equal(150m, s.Amount));
    }
}
