using Owezy.Domain.Auth;

namespace Owezy.Domain.Billing;

public sealed class Bill
{
    private readonly List<Participant> _participants = new();
    private readonly List<BillItem> _items = new();
    private readonly List<ParticipantAccessLink> _accessLinks = new();

    public BillId Id { get; }
    public string Title { get; }
    public PhoneNumber SplitterPhoneNumber { get; }
    public DateTimeOffset CreatedAt { get; }
    public BillStatus Status { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }

    public bool IsOpen => Status == BillStatus.Active;
    public bool IsFinalized => Status == BillStatus.Finalized;

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();
    public IReadOnlyCollection<BillItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<ParticipantAccessLink> AccessLinks => _accessLinks.AsReadOnly();

    private Bill(
        BillId id,
        string title,
        PhoneNumber splitterPhoneNumber,
        DateTimeOffset createdAt,
        BillStatus status,
        DateTimeOffset? finalizedAt = null)
    {
        Id = id;
        Title = title;
        SplitterPhoneNumber = splitterPhoneNumber;
        CreatedAt = createdAt;
        Status = status;
        FinalizedAt = finalizedAt;
    }

    public static Bill Create(string title, PhoneNumber splitterPhoneNumber, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Bill title cannot be null or empty.", nameof(title));
        }
        ArgumentNullException.ThrowIfNull(splitterPhoneNumber);

        var bill = new Bill(
            BillId.New(),
            title.Trim(),
            splitterPhoneNumber,
            now,
            BillStatus.Active
        );

        // Splitter is automatically added as the first participant
        var splitterParticipant = Participant.Create(bill.Id, splitterPhoneNumber, now);
        bill._participants.Add(splitterParticipant);

        return bill;
    }

    public static Bill Reconstitute(
        BillId id,
        string title,
        PhoneNumber splitterPhoneNumber,
        DateTimeOffset createdAt,
        BillStatus status,
        IEnumerable<Participant> participants,
        IEnumerable<BillItem>? items = null,
        DateTimeOffset? finalizedAt = null,
        IEnumerable<ParticipantAccessLink>? accessLinks = null)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("BillId cannot be empty.", nameof(id));
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        }
        ArgumentNullException.ThrowIfNull(splitterPhoneNumber);

        var bill = new Bill(id, title.Trim(), splitterPhoneNumber, createdAt, status, finalizedAt);
        if (participants is not null)
        {
            bill._participants.AddRange(participants);
        }
        if (items is not null)
        {
            bill._items.AddRange(items);
        }
        if (accessLinks is not null)
        {
            bill._accessLinks.AddRange(accessLinks);
        }
        return bill;
    }

    public Participant AddParticipant(PhoneNumber phoneNumber, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        if (IsFinalized)
        {
            throw new InvalidOperationException("Cannot add a participant to a finalized bill.");
        }

        if (_participants.Any(p => p.PhoneNumber == phoneNumber))
        {
            throw new InvalidOperationException($"Participant with phone number {phoneNumber.Value} is already a member of this bill.");
        }

        var participant = Participant.Create(Id, phoneNumber, now);
        _participants.Add(participant);
        return participant;
    }

    public BillItem AddItem(
        string description,
        int quantity,
        decimal amount,
        IEnumerable<ParticipantId> sharerParticipantIds)
    {
        if (IsFinalized)
        {
            throw new InvalidOperationException("Cannot add an item to a finalized bill.");
        }

        ArgumentNullException.ThrowIfNull(sharerParticipantIds);

        var sharerList = sharerParticipantIds.ToList();

        // Enforce invariant: Every sharer MUST belong to THIS bill!
        foreach (var sharerId in sharerList)
        {
            if (!_participants.Any(p => p.Id == sharerId))
            {
                throw new ArgumentException($"Participant '{sharerId}' does not belong to bill '{Id}'.", nameof(sharerParticipantIds));
            }
        }

        var item = BillItem.Create(Id, description, quantity, amount, sharerList);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Finalizes the bill, making it permanently immutable.
    /// Requires at least one participant and at least one item.
    /// </summary>
    public void Finalize(DateTimeOffset now)
    {
        if (IsFinalized)
        {
            throw new InvalidOperationException("Bill is already finalized.");
        }

        if (_participants.Count == 0)
        {
            throw new InvalidOperationException("A bill must have at least one participant before it can be finalized.");
        }

        if (_items.Count == 0)
        {
            throw new InvalidOperationException("A bill must have at least one item before it can be finalized.");
        }

        Status = BillStatus.Finalized;
        FinalizedAt = now;
    }

    /// <summary>
    /// Generates a participant access link for a finalized bill.
    /// Invariants enforced:
    /// 1. Bill MUST be finalized (OPEN bills cannot generate access links).
    /// 2. Participant MUST belong to this bill.
    /// Revokes any previous active link for the participant.
    /// </summary>
    public ParticipantAccessLink GenerateAccessLink(ParticipantId participantId, string tokenHash, DateTimeOffset now)
    {
        if (!IsFinalized)
        {
            throw new InvalidOperationException("Participant access links can only be generated for finalized bills.");
        }

        if (!_participants.Any(p => p.Id == participantId))
        {
            throw new ArgumentException($"Participant '{participantId}' does not belong to bill '{Id}'.", nameof(participantId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("TokenHash cannot be null or empty.", nameof(tokenHash));
        }

        // Revoke existing active links for this participant
        foreach (var existing in _accessLinks.Where(l => l.ParticipantId == participantId && !l.IsRevoked))
        {
            existing.Revoke();
        }

        var link = ParticipantAccessLink.Create(Id, participantId, tokenHash, now);
        _accessLinks.Add(link);
        return link;
    }

    /// <summary>
    /// Marks a participant as paid on a finalized bill.
    /// Invariants enforced:
    /// 1. Bill MUST be finalized (OPEN bills cannot track payment status).
    /// 2. Participant MUST belong to this bill.
    /// </summary>
    public void MarkParticipantPaid(ParticipantId participantId, DateTimeOffset now)
    {
        if (!IsFinalized)
        {
            throw new InvalidOperationException("Payment status can only be updated for finalized bills.");
        }

        var participant = _participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null)
        {
            throw new ArgumentException($"Participant '{participantId}' does not belong to bill '{Id}'.", nameof(participantId));
        }

        participant.MarkPaid(now);
    }
}
