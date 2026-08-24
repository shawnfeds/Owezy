using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class BillServiceFinalizeTests
{
    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();

        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default)
        {
            Store.TryGetValue(id, out var b); return Task.FromResult(b);
        }

        public Task AddAsync(Bill bill, CancellationToken ct = default)
        {
            Store[bill.Id] = bill; return Task.CompletedTask;
        }

        public Task UpdateAsync(Bill bill, CancellationToken ct = default)
        {
            Store[bill.Id] = bill; return Task.CompletedTask;
        }
    }

    private class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private readonly InMemoryBillRepository _repo = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly PhoneNumber _splitterPhone = PhoneNumber.Create("+919876543210");
    private readonly PhoneNumber _otherPhone = PhoneNumber.Create("+919123456789");

    private async Task<BillId> CreateBillWithItemAsync(BillService svc)
    {
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Lunch"));
        var bill = await _repo.GetByIdAsync(createResult.BillId);
        var splitterPart = bill!.Participants.First();
        await svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(
            createResult.BillId, "Salad", 1, 200.00m, new[] { splitterPart.Id }));
        return createResult.BillId;
    }

    [Fact]
    public async Task FinalizeBillAsync_AuthenticatedSplitter_FinalizesSuccessfully()
    {
        var svc = new BillService(_repo, _clock);
        var billId = await CreateBillWithItemAsync(svc);

        var result = await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(billId));

        Assert.Equal(BillStatus.Finalized, result.Status);
        Assert.NotNull(result.FinalizedAt);

        var updated = await _repo.GetByIdAsync(billId);
        Assert.True(updated!.IsFinalized);
    }

    [Fact]
    public async Task FinalizeBillAsync_NonSplitterCaller_ThrowsUnauthorized()
    {
        var svc = new BillService(_repo, _clock);
        var billId = await CreateBillWithItemAsync(svc);
        // Add the other phone as participant so the bill is valid
        await svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(billId, _otherPhone));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.FinalizeBillAsync(_otherPhone, new FinalizeBillRequest(billId)));
    }

    [Fact]
    public async Task FinalizeBillAsync_BillNotFound_ThrowsKeyNotFoundException()
    {
        var svc = new BillService(_repo, _clock);
        var missingId = BillId.New();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(missingId)));
    }

    [Fact]
    public async Task FinalizeBillAsync_AlreadyFinalized_ThrowsInvalidOperationException()
    {
        var svc = new BillService(_repo, _clock);
        var billId = await CreateBillWithItemAsync(svc);
        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(billId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(billId)));
    }

    [Fact]
    public async Task FinalizeBillAsync_BillWithNoItems_ThrowsInvalidOperationException()
    {
        var svc = new BillService(_repo, _clock);
        var createResult = await svc.CreateBillAsync(_splitterPhone, new CreateBillRequest("Empty Bill"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(createResult.BillId)));
    }

    [Fact]
    public async Task AddParticipantAsync_FinalizedBill_ThrowsInvalidOperationException()
    {
        var svc = new BillService(_repo, _clock);
        var billId = await CreateBillWithItemAsync(svc);
        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(billId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddParticipantAsync(_splitterPhone, new AddParticipantRequest(billId, _otherPhone)));
    }

    [Fact]
    public async Task AddBillItemAsync_FinalizedBill_ThrowsInvalidOperationException()
    {
        var svc = new BillService(_repo, _clock);
        var billId = await CreateBillWithItemAsync(svc);
        await svc.FinalizeBillAsync(_splitterPhone, new FinalizeBillRequest(billId));
        var bill = await _repo.GetByIdAsync(billId);
        var splitterPart = bill!.Participants.First();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddBillItemAsync(_splitterPhone, new AddBillItemRequest(
                billId, "Drink", 1, 50.00m, new[] { splitterPart.Id })));
    }
}
