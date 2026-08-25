namespace Owezy.Infrastructure.Persistence;

public sealed class BillParticipantRow
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }

    public BillRow? Bill { get; set; }
}

public sealed class BillRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SplitterPhoneNumber { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }

    public ICollection<BillParticipantRow> Participants { get; set; } = new List<BillParticipantRow>();
    public ICollection<BillItemRow> Items { get; set; } = new List<BillItemRow>();
    public ICollection<ParticipantAccessLinkRow> AccessLinks { get; set; } = new List<ParticipantAccessLinkRow>();
}
