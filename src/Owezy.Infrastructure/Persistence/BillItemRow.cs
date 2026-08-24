namespace Owezy.Infrastructure.Persistence;

public sealed class BillItemRow
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }

    public BillRow? Bill { get; set; }
    public ICollection<BillItemSharerRow> Sharers { get; set; } = new List<BillItemSharerRow>();
}

public sealed class BillItemSharerRow
{
    public Guid ItemId { get; set; }
    public Guid ParticipantId { get; set; }

    public BillItemRow? Item { get; set; }
    public BillParticipantRow? Participant { get; set; }
}
