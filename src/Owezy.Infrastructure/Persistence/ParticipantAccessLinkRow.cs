namespace Owezy.Infrastructure.Persistence;

public sealed class ParticipantAccessLinkRow
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public Guid ParticipantId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRevoked { get; set; }

    public BillRow? Bill { get; set; }
}
