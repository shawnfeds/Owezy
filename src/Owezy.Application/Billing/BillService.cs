using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed class BillService : IBillService
{
    private readonly IBillRepository _billRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BillService(IBillRepository billRepository, IDateTimeProvider dateTimeProvider)
    {
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    public async Task<CreateBillResult> CreateBillAsync(
        PhoneNumber splitterPhoneNumber,
        CreateBillRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(splitterPhoneNumber);
        ArgumentNullException.ThrowIfNull(request);

        var now = _dateTimeProvider.UtcNow;
        var bill = Bill.Create(request.Title, splitterPhoneNumber, now);

        await _billRepository.AddAsync(bill, cancellationToken);

        return new CreateBillResult(
            bill.Id,
            bill.Title,
            bill.SplitterPhoneNumber,
            bill.Participants.Count,
            bill.CreatedAt
        );
    }

    public async Task<AddParticipantResult> AddParticipantAsync(
        PhoneNumber callerPhoneNumber,
        AddParticipantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.PhoneNumber);

        if (request.BillId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(request));
        }

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{request.BillId}' was not found.");
        }

        // Caller must be a member of the bill (splitter or participant)
        if (!bill.Participants.Any(p => p.PhoneNumber == callerPhoneNumber))
        {
            throw new UnauthorizedAccessException("Only existing members of a bill can add new participants.");
        }

        var now = _dateTimeProvider.UtcNow;
        var participant = bill.AddParticipant(request.PhoneNumber, now);

        await _billRepository.UpdateAsync(bill, cancellationToken);

        return new AddParticipantResult(
            participant.Id,
            participant.BillId,
            participant.PhoneNumber,
            participant.JoinedAt
        );
    }

    public async Task<AddBillItemResult> AddBillItemAsync(
        PhoneNumber callerPhoneNumber,
        AddBillItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        ArgumentNullException.ThrowIfNull(request);

        if (request.BillId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(request));
        }

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{request.BillId}' was not found.");
        }

        // Section 24 API Ownership: Only the authenticated splitter can add items to the bill
        if (bill.SplitterPhoneNumber != callerPhoneNumber)
        {
            throw new UnauthorizedAccessException("Only the bill splitter can add items to the bill.");
        }

        var item = bill.AddItem(
            request.Description,
            request.Quantity,
            request.Amount,
            request.SharerParticipantIds
        );

        await _billRepository.UpdateAsync(bill, cancellationToken);

        return new AddBillItemResult(
            item.Id,
            item.BillId,
            item.Description,
            item.Quantity,
            item.Amount,
            item.SharerParticipantIds
        );
    }
}
