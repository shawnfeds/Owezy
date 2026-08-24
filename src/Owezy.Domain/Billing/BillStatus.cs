namespace Owezy.Domain.Billing;

public enum BillStatus
{
    /// <summary>The bill is open and can be edited by the splitter.</summary>
    Active = 1,

    /// <summary>The bill is finalized. Contents are locked and immutable.</summary>
    Finalized = 2
}
