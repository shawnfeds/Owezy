using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class BillServiceItemTests
{
    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();

        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(id, out var bill);
            return Task.FromResult(bill);
        }

        public Task<Bill?> GetByAccessLinkHashAsync(string tokenHash, CancellationToken ct = default)
        {
            var bill = Store.Values.FirstOrDefault(b => b.AccessLinks.Any(l => l.TokenHash == tokenHash && !l.IsRevoked));
            return Task.FromResult(bill);
        }

        public Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
        {
            Store[bill.Id] = bill;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default)
        {
            Store[bill.Id] = bill;
            return Task.CompletedTask;
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 24, 15, 0, 0, TimeSpan.Zero);
    }

    private readonly InMemoryBillRepository _billRepository = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new();
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _participantPhone = PhoneNumber.Create("+919123456789");

    [Fact]
    public async Task AddBillItemAsync_AuthenticatedSplitter_AddsItemAndPersists()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);
        var createResult = await service.CreateBillAsync(_splitterPhone, new CreateBillRequest("Team Dinner"));

        var bill = await _billRepository.GetByIdAsync(createResult.BillId);
        var splitterParticipant = bill!.Participants.First();

        var request = new AddBillItemRequest(
            createResult.BillId,
            "Shared Appetizer",
            1,
            450m,
            new[] { splitterParticipant.Id }
        );

        var result = await service.AddBillItemAsync(_splitterPhone, request);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ItemId.Value);
        Assert.Equal("Shared Appetizer", result.Description);
        Assert.Equal(450m, result.Amount);
        Assert.Single(result.SharerParticipantIds);

        var updatedBill = await _billRepository.GetByIdAsync(createResult.BillId);
        Assert.Single(updatedBill!.Items);
    }

    [Fact]
    public async Task AddBillItemAsync_NonSplitterCaller_ThrowsUnauthorizedAccessException()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);
        var createResult = await service.CreateBillAsync(_splitterPhone, new CreateBillRequest("Team Dinner"));

        // Add a participant to the bill
        var partResult = await service.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(createResult.BillId, _participantPhone));
        var bill = await _billRepository.GetByIdAsync(createResult.BillId);
        var participant = bill!.Participants.First(p => p.PhoneNumber == _participantPhone);

        var request = new AddBillItemRequest(
            createResult.BillId,
            "Dessert",
            1,
            200m,
            new[] { participant.Id }
        );

        // Non-splitter participant attempts to add item
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddBillItemAsync(_participantPhone, request));
    }

    [Fact]
    public async Task AddBillItemAsync_CrossBillSharer_ThrowsArgumentException()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);

        var bill1Result = await service.CreateBillAsync(_splitterPhone, new CreateBillRequest("Bill 1"));
        var bill2Result = await service.CreateBillAsync(PhoneNumber.Create("+918888888888"), new CreateBillRequest("Bill 2"));

        var bill2 = await _billRepository.GetByIdAsync(bill2Result.BillId);
        var bill2Participant = bill2!.Participants.First();

        var request = new AddBillItemRequest(
            bill1Result.BillId,
            "Item",
            1,
            100m,
            new[] { bill2Participant.Id } // Belongs to Bill 2!
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddBillItemAsync(_splitterPhone, request));
    }
}
