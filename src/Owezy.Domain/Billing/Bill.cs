using Owezy.Domain.Auth;

namespace Owezy.Domain.Billing;

public sealed class Bill
{
    private readonly List<Participant> _participants = new();
    private readonly List<BillItem> _items = new();

    public BillId Id { get; }
    public string Title { get; }
    public PhoneNumber SplitterPhoneNumber { get; }
    public DateTimeOffset CreatedAt { get; }
    public BillStatus Status { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();
    public IReadOnlyCollection<BillItem> Items => _items.AsReadOnly();

    private Bill(
        BillId id,
        string title,
        PhoneNumber splitterPhoneNumber,
        DateTimeOffset createdAt,
        BillStatus status)
    {
        Id = id;
        Title = title;
        SplitterPhoneNumber = splitterPhoneNumber;
        CreatedAt = createdAt;
        Status = status;
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
        IEnumerable<BillItem>? items = null)
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

        var bill = new Bill(id, title.Trim(), splitterPhoneNumber, createdAt, status);
        if (participants is not null)
        {
            bill._participants.AddRange(participants);
        }
        if (items is not null)
        {
            bill._items.AddRange(items);
        }
        return bill;
    }

    public Participant AddParticipant(PhoneNumber phoneNumber, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

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
}
