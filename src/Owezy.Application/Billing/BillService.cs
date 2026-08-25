using Owezy.Application.Common;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Application.Billing;

public sealed class BillService : IBillService
{
    private readonly IBillRepository _billRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IParticipantTokenGenerator? _tokenGenerator;

    public BillService(
        IBillRepository billRepository,
        IDateTimeProvider dateTimeProvider,
        IParticipantTokenGenerator? tokenGenerator = null)
    {
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _tokenGenerator = tokenGenerator;
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

    public async Task<CalculateItemSharesResult> CalculateItemSharesAsync(
        PhoneNumber callerPhoneNumber,
        CalculateItemSharesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        ArgumentNullException.ThrowIfNull(request);

        if (request.BillId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(request));
        }
        if (request.ItemId.Value == Guid.Empty)
        {
            throw new ArgumentException("ItemId cannot be empty.", nameof(request));
        }

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{request.BillId}' was not found.");
        }

        // Caller must be a member of the bill (splitter or participant)
        if (!bill.Participants.Any(p => p.PhoneNumber == callerPhoneNumber))
        {
            throw new UnauthorizedAccessException("Only bill members can view calculated shares.");
        }

        var item = bill.Items.FirstOrDefault(i => i.Id == request.ItemId);
        if (item is null)
        {
            throw new KeyNotFoundException($"Item with ID '{request.ItemId}' was not found on bill '{request.BillId}'.");
        }

        var shares = EqualSplitCalculator.Calculate(item);
        var shareResults = shares.Select(s => new ParticipantShareResult(s.ParticipantId, s.Amount)).ToList().AsReadOnly();

        return new CalculateItemSharesResult(
            bill.Id,
            item.Id,
            item.Description,
            item.Amount,
            shareResults
        );
    }

    public async Task<FinalizeBillResult> FinalizeBillAsync(
        PhoneNumber callerPhoneNumber,
        FinalizeBillRequest request,
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

        // Only the authenticated splitter can finalize
        if (bill.SplitterPhoneNumber != callerPhoneNumber)
        {
            throw new UnauthorizedAccessException("Only the bill splitter can finalize the bill.");
        }

        var now = _dateTimeProvider.UtcNow;
        bill.Finalize(now);

        await _billRepository.UpdateAsync(bill, cancellationToken);

        return new FinalizeBillResult(
            bill.Id,
            bill.Title,
            bill.Status,
            bill.FinalizedAt
        );
    }

    public async Task<GenerateParticipantAccessLinkResult> GenerateParticipantAccessLinkAsync(
        PhoneNumber callerPhoneNumber,
        GenerateParticipantAccessLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        ArgumentNullException.ThrowIfNull(request);

        if (request.BillId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(request));
        }
        if (request.ParticipantId.Value == Guid.Empty)
        {
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(request));
        }

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{request.BillId}' was not found.");
        }

        // Only the authenticated splitter can generate access links
        if (bill.SplitterPhoneNumber != callerPhoneNumber)
        {
            throw new UnauthorizedAccessException("Only the bill splitter can generate participant access links.");
        }

        var tokenGen = _tokenGenerator ?? throw new InvalidOperationException("IParticipantTokenGenerator is not configured.");
        var rawToken = tokenGen.GenerateToken();
        var tokenHash = tokenGen.HashToken(rawToken);

        var now = _dateTimeProvider.UtcNow;
        var link = bill.GenerateAccessLink(request.ParticipantId, tokenHash, now);

        await _billRepository.UpdateAsync(bill, cancellationToken);

        return new GenerateParticipantAccessLinkResult(
            rawToken,
            bill.Id,
            request.ParticipantId,
            link.CreatedAt
        );
    }

    public async Task<ParticipantBillViewResult?> GetParticipantViewAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenGen = _tokenGenerator ?? throw new InvalidOperationException("IParticipantTokenGenerator is not configured.");
        var tokenHash = tokenGen.HashToken(rawToken.Trim());

        var bill = await _billRepository.GetByAccessLinkHashAsync(tokenHash, cancellationToken);
        if (bill is null || !bill.IsFinalized)
        {
            // Participant links are only accessible for FINALIZED bills
            return null;
        }

        var link = bill.AccessLinks.FirstOrDefault(l => l.TokenHash == tokenHash && !l.IsRevoked);
        if (link is null)
        {
            return null;
        }

        var targetParticipant = bill.Participants.FirstOrDefault(p => p.Id == link.ParticipantId);
        if (targetParticipant is null)
        {
            return null;
        }

        // Calculate participant's item shares using EqualSplitCalculator
        decimal billTotalAmount = 0m;
        decimal totalAmountOwed = 0m;
        var participantItems = new List<ParticipantItemShareDto>();

        foreach (var item in bill.Items)
        {
            billTotalAmount += item.Amount;

            if (item.SharerParticipantIds.Contains(targetParticipant.Id))
            {
                var shares = EqualSplitCalculator.Calculate(item.Amount, item.SharerParticipantIds);
                var myShare = shares.First(s => s.ParticipantId == targetParticipant.Id);
                totalAmountOwed += myShare.Amount;
                participantItems.Add(new ParticipantItemShareDto(
                    item.Description,
                    item.Quantity,
                    item.Amount,
                    myShare.Amount
                ));
            }
        }

        return new ParticipantBillViewResult(
            bill.Id,
            bill.Title,
            billTotalAmount,
            targetParticipant.Id,
            targetParticipant.PhoneNumber,
            totalAmountOwed,
            targetParticipant.PaymentStatus,
            targetParticipant.PaidAt,
            participantItems
        );
    }

    public async Task<MarkParticipantPaidResult?> MarkParticipantPaidByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenGen = _tokenGenerator ?? throw new InvalidOperationException("IParticipantTokenGenerator is not configured.");
        var tokenHash = tokenGen.HashToken(rawToken.Trim());

        var bill = await _billRepository.GetByAccessLinkHashAsync(tokenHash, cancellationToken);
        if (bill is null || !bill.IsFinalized)
        {
            return null;
        }

        var link = bill.AccessLinks.FirstOrDefault(l => l.TokenHash == tokenHash && !l.IsRevoked);
        if (link is null)
        {
            return null;
        }

        var participant = bill.Participants.FirstOrDefault(p => p.Id == link.ParticipantId);
        if (participant is null)
        {
            return null;
        }

        var now = _dateTimeProvider.UtcNow;
        bill.MarkParticipantPaid(participant.Id, now);

        await _billRepository.UpdateAsync(bill, cancellationToken);

        return new MarkParticipantPaidResult(
            participant.Id,
            participant.PaymentStatus,
            participant.PaidAt
        );
    }

    public async Task<SplitterBillPaymentsResult> GetSplitterBillPaymentsAsync(
        PhoneNumber callerPhoneNumber,
        BillId billId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }

        var bill = await _billRepository.GetByIdAsync(billId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{billId}' was not found.");
        }

        if (bill.SplitterPhoneNumber != callerPhoneNumber)
        {
            throw new UnauthorizedAccessException("Only the bill splitter can view payment status for the bill.");
        }

        if (!bill.IsFinalized)
        {
            throw new InvalidOperationException("Payment status is only available for finalized bills.");
        }

        decimal billTotalAmount = bill.Items.Sum(i => i.Amount);
        var participantPayments = new List<ParticipantPaymentStatusDto>();

        foreach (var participant in bill.Participants)
        {
            decimal participantTotalOwed = 0m;
            foreach (var item in bill.Items)
            {
                if (item.SharerParticipantIds.Contains(participant.Id))
                {
                    var shares = EqualSplitCalculator.Calculate(item.Amount, item.SharerParticipantIds);
                    var share = shares.First(s => s.ParticipantId == participant.Id);
                    participantTotalOwed += share.Amount;
                }
            }

            participantPayments.Add(new ParticipantPaymentStatusDto(
                participant.Id,
                participant.PhoneNumber,
                participantTotalOwed,
                participant.PaymentStatus,
                participant.PaidAt
            ));
        }

        return new SplitterBillPaymentsResult(
            bill.Id,
            bill.Title,
            billTotalAmount,
            participantPayments
        );
    }

    public async Task<BillSettlementResult> GetBillSettlementAsync(
        PhoneNumber callerPhoneNumber,
        BillId billId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }

        var bill = await _billRepository.GetByIdAsync(billId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{billId}' was not found.");
        }

        if (bill.SplitterPhoneNumber != callerPhoneNumber)
        {
            throw new UnauthorizedAccessException("Only the bill splitter can request the settlement summary.");
        }

        if (!bill.IsFinalized)
        {
            throw new InvalidOperationException("Settlement is only available for finalized bills.");
        }

        decimal billTotalAmount = bill.Items.Sum(i => i.Amount);
        var participantSettlements = new List<ParticipantSettlementDto>();

        foreach (var participant in bill.Participants)
        {
            // Derive this participant's calculated share
            decimal amountOwed = 0m;
            foreach (var item in bill.Items)
            {
                if (item.SharerParticipantIds.Contains(participant.Id))
                {
                    var shares = EqualSplitCalculator.Calculate(item.Amount, item.SharerParticipantIds);
                    var share = shares.First(s => s.ParticipantId == participant.Id);
                    amountOwed += share.Amount;
                }
            }

            var amountPaid = participant.PaymentStatus == PaymentStatus.Paid ? amountOwed : 0m;
            var amountRemaining = amountOwed - amountPaid;

            participantSettlements.Add(new ParticipantSettlementDto(
                participant.Id,
                participant.PhoneNumber,
                amountOwed,
                amountPaid,
                amountRemaining,
                participant.PaymentStatus
            ));
        }

        var totalOwed = participantSettlements.Sum(p => p.AmountOwed);
        var totalPaid = participantSettlements.Sum(p => p.AmountPaid);
        var totalRemaining = participantSettlements.Sum(p => p.AmountRemaining);
        var paidCount = participantSettlements.Count(p => p.PaymentStatus == PaymentStatus.Paid);
        var unpaidCount = participantSettlements.Count(p => p.PaymentStatus == PaymentStatus.Unpaid);

        return new BillSettlementResult(
            bill.Id,
            bill.Title,
            billTotalAmount,
            totalOwed,
            totalPaid,
            totalRemaining,
            participantSettlements.Count,
            paidCount,
            unpaidCount,
            participantSettlements
        );
    }

    public async Task<UpdateItemSharersResult> UpdateItemSharersAsync(
        PhoneNumber callerPhoneNumber,
        UpdateItemSharersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callerPhoneNumber);
        ArgumentNullException.ThrowIfNull(request);

        if (request.BillId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(request));
        }
        if (request.ItemId.Value == Guid.Empty)
        {
            throw new ArgumentException("ItemId cannot be empty.", nameof(request));
        }

        var bill = await _billRepository.GetByIdAsync(request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new KeyNotFoundException($"Bill with ID '{request.BillId}' was not found.");
        }

        if (bill.SplitterPhoneNumber != callerPhoneNumber)
        {
            throw new UnauthorizedAccessException("Only the bill splitter can modify item sharers.");
        }

        bill.UpdateItemSharers(request.ItemId, request.SharerParticipantIds);

        await _billRepository.UpdateAsync(bill, cancellationToken);

        var updatedItem = bill.Items.First(i => i.Id == request.ItemId);

        return new UpdateItemSharersResult(
            updatedItem.Id,
            updatedItem.BillId,
            updatedItem.SharerParticipantIds
        );
    }
}
