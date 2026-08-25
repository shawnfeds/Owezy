namespace Owezy.Domain.Billing;

public sealed class BillItem
{
    private readonly HashSet<ParticipantId> _sharerParticipantIds = new();

    public BillItemId Id { get; }
    public BillId BillId { get; }
    public string Description { get; }
    public int Quantity { get; }
    public decimal Amount { get; }

    public IReadOnlyCollection<ParticipantId> SharerParticipantIds => _sharerParticipantIds;

    private BillItem(
        BillItemId id,
        BillId billId,
        string description,
        int quantity,
        decimal amount,
        IEnumerable<ParticipantId> sharerParticipantIds)
    {
        Id = id;
        BillId = billId;
        Description = description;
        Quantity = quantity;
        Amount = amount;
        foreach (var sharerId in sharerParticipantIds)
        {
            _sharerParticipantIds.Add(sharerId);
        }
    }

    public static BillItem Create(
        BillId billId,
        string description,
        int quantity,
        decimal amount,
        IEnumerable<ParticipantId> sharerParticipantIds)
    {
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Item description cannot be empty.", nameof(description));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(sharerParticipantIds);

        var sharerList = sharerParticipantIds.ToList();

        var uniqueSharerSet = new HashSet<ParticipantId>();
        foreach (var sharerId in sharerList)
        {
            if (sharerId.Value == Guid.Empty)
            {
                throw new ArgumentException("Sharer ParticipantId cannot be empty.", nameof(sharerParticipantIds));
            }

            if (!uniqueSharerSet.Add(sharerId))
            {
                throw new ArgumentException($"Duplicate sharer participant ID '{sharerId}' for item.", nameof(sharerParticipantIds));
            }
        }

        return new BillItem(
            BillItemId.New(),
            billId,
            description.Trim(),
            quantity,
            amount,
            uniqueSharerSet
        );
    }

    public static BillItem Reconstitute(
        BillItemId id,
        BillId billId,
        string description,
        int quantity,
        decimal amount,
        IEnumerable<ParticipantId> sharerParticipantIds)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("BillItemId cannot be empty.", nameof(id));
        }
        if (billId.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(billId));
        }

        return new BillItem(id, billId, description.Trim(), quantity, amount, sharerParticipantIds ?? Enumerable.Empty<ParticipantId>());
    }
}
