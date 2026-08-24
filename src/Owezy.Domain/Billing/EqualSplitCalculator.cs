namespace Owezy.Domain.Billing;

public static class EqualSplitCalculator
{
    public static IReadOnlyList<ParticipantShare> Calculate(BillItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Calculate(item.Amount, item.SharerParticipantIds);
    }

    public static IReadOnlyList<ParticipantShare> Calculate(decimal amount, IEnumerable<ParticipantId> sharerParticipantIds)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (decimal.Remainder(amount * 100m, 1m) != 0m)
        {
            throw new ArgumentException("Monetary amount cannot have more than 2 decimal places.", nameof(amount));
        }

        ArgumentNullException.ThrowIfNull(sharerParticipantIds);

        var sharerList = sharerParticipantIds.ToList();
        if (sharerList.Count == 0)
        {
            throw new ArgumentException("At least one sharer participant is required.", nameof(sharerParticipantIds));
        }

        var uniqueSet = new HashSet<ParticipantId>();
        foreach (var s in sharerList)
        {
            if (s.Value == Guid.Empty)
            {
                throw new ArgumentException("ParticipantId cannot be empty.", nameof(sharerParticipantIds));
            }
            if (!uniqueSet.Add(s))
            {
                throw new ArgumentException($"Duplicate participant ID '{s}' found in sharers.", nameof(sharerParticipantIds));
            }
        }

        int count = uniqueSet.Count;

        // Equal share calculation using Largest Remainder Method
        decimal exactShare = amount / count;
        decimal baseShare = Math.Floor(exactShare * 100m) / 100m;
        decimal baseTotal = baseShare * count;
        decimal remainderTotal = amount - baseTotal;

        int extraCentCount = (int)Math.Round(remainderTotal * 100m);

        // Deterministic ordering:
        // 1. Remainder DESC (for equal splits, mathematical remainders are equal)
        // 2. ParticipantId ASC (Guid value ascending)
        var orderedSharers = uniqueSet
            .OrderBy(id => id.Value)
            .ToList();

        var result = new List<ParticipantShare>(count);
        for (int i = 0; i < count; i++)
        {
            var participantId = orderedSharers[i];
            decimal finalShare = (i < extraCentCount) ? baseShare + 0.01m : baseShare;
            result.Add(new ParticipantShare(participantId, finalShare));
        }

        return result.AsReadOnly();
    }
}
