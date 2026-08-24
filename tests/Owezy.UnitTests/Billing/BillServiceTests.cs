using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class BillServiceTests
{
    private class InMemoryBillRepository : IBillRepository
    {
        public Dictionary<BillId, Bill> Store { get; } = new();

        public Task<Bill?> GetByIdAsync(BillId id, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(id, out var bill);
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
    public async Task CreateBillAsync_ValidInput_CreatesBillAndPersists()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);
        var request = new CreateBillRequest("Lunch Split");

        var result = await service.CreateBillAsync(_splitterPhone, request);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.BillId.Value);
        Assert.Equal("Lunch Split", result.Title);
        Assert.Equal(_splitterPhone, result.SplitterPhoneNumber);
        Assert.Equal(1, result.ParticipantCount); // Splitter is initial participant

        var stored = await _billRepository.GetByIdAsync(result.BillId);
        Assert.NotNull(stored);
        Assert.Equal("Lunch Split", stored.Title);
    }

    [Fact]
    public async Task AddParticipantAsync_ExistingMemberCaller_AddsParticipant()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);
        var createResult = await service.CreateBillAsync(_splitterPhone, new CreateBillRequest("Dinner"));

        var addRequest = new AddParticipantRequest(createResult.BillId, _participantPhone);
        var addResult = await service.AddParticipantAsync(_splitterPhone, addRequest);

        Assert.NotNull(addResult);
        Assert.Equal(_participantPhone, addResult.PhoneNumber);

        var stored = await _billRepository.GetByIdAsync(createResult.BillId);
        Assert.Equal(2, stored!.Participants.Count);
    }

    [Fact]
    public async Task AddParticipantAsync_NonMemberCaller_ThrowsUnauthorizedAccessException()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);
        var createResult = await service.CreateBillAsync(_splitterPhone, new CreateBillRequest("Dinner"));

        var outsiderPhone = PhoneNumber.Create("+918888888888");
        var addRequest = new AddParticipantRequest(createResult.BillId, _participantPhone);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddParticipantAsync(outsiderPhone, addRequest));
    }

    [Fact]
    public async Task AddParticipantAsync_DuplicateParticipant_ThrowsInvalidOperationException()
    {
        var service = new BillService(_billRepository, _dateTimeProvider);
        var createResult = await service.CreateBillAsync(_splitterPhone, new CreateBillRequest("Dinner"));

        // Splitter is already a participant
        var addSplitterAgain = new AddParticipantRequest(createResult.BillId, _splitterPhone);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddParticipantAsync(_splitterPhone, addSplitterAgain));
    }
}
