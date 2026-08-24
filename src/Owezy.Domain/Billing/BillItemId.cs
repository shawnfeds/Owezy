namespace Owezy.Domain.Billing;

public readonly record struct BillItemId(Guid Value)
{
    public static BillItemId New() => new(Guid.NewGuid());
    public static BillItemId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();
}
