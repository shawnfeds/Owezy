namespace Owezy.Domain.Receipts;

public readonly record struct ReceiptId(Guid Value)
{
    public static ReceiptId New() => new(Guid.NewGuid());
    public static ReceiptId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();
}
