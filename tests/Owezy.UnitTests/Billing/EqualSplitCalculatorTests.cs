using Owezy.Domain.Billing;
using Xunit;

namespace Owezy.UnitTests.Billing;

public class EqualSplitCalculatorTests
{
    private static (ParticipantId A, ParticipantId B, ParticipantId C) CreateSortedParticipants()
    {
        var g1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var g2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var g3 = Guid.Parse("00000000-0000-0000-0000-000000000003");

        return (new ParticipantId(g1), new ParticipantId(g2), new ParticipantId(g3));
    }

    [Fact(DisplayName = "TEST A: Amount 100.00 divided among 3 sharers results in 33.34, 33.33, 33.33")]
    public void Calculate_100_00_ThreeSharers_Returns33_34_And_33_33_And_33_33()
    {
        var (pA, pB, pC) = CreateSortedParticipants();
        var shares = EqualSplitCalculator.Calculate(100.00m, new[] { pA, pB, pC });

        Assert.Equal(3, shares.Count);

        var shareA = shares.First(s => s.ParticipantId == pA);
        var shareB = shares.First(s => s.ParticipantId == pB);
        var shareC = shares.First(s => s.ParticipantId == pC);

        Assert.Equal(33.34m, shareA.Amount);
        Assert.Equal(33.33m, shareB.Amount);
        Assert.Equal(33.33m, shareC.Amount);

        Assert.Equal(100.00m, shares.Sum(s => s.Amount));
    }

    [Fact(DisplayName = "TEST B: Amount 10.01 divided among 2 sharers gives extra cent to smaller ParticipantId")]
    public void Calculate_10_01_TwoSharers_DeterministicTieBreaking()
    {
        var (pA, pB, _) = CreateSortedParticipants(); // pA.Value < pB.Value
        var shares = EqualSplitCalculator.Calculate(10.01m, new[] { pB, pA }); // Note reverse input order

        var shareA = shares.First(s => s.ParticipantId == pA);
        var shareB = shares.First(s => s.ParticipantId == pB);

        Assert.Equal(5.01m, shareA.Amount);
        Assert.Equal(5.00m, shareB.Amount);

        Assert.Equal(10.01m, shares.Sum(s => s.Amount));
    }

    [Fact(DisplayName = "TEST C: Amount 10.00 divided among 3 sharers results in 3.34, 3.33, 3.33")]
    public void Calculate_10_00_ThreeSharers_Returns3_34_And_3_33_And_3_33()
    {
        var (pA, pB, pC) = CreateSortedParticipants();
        var shares = EqualSplitCalculator.Calculate(10.00m, new[] { pA, pB, pC });

        var shareA = shares.First(s => s.ParticipantId == pA);
        var shareB = shares.First(s => s.ParticipantId == pB);
        var shareC = shares.First(s => s.ParticipantId == pC);

        Assert.Equal(3.34m, shareA.Amount);
        Assert.Equal(3.33m, shareB.Amount);
        Assert.Equal(3.33m, shareC.Amount);

        Assert.Equal(10.00m, shares.Sum(s => s.Amount));
    }

    [Fact(DisplayName = "TEST D: Amount 10.02 divided among 3 sharers results in 3.34, 3.34, 3.34")]
    public void Calculate_10_02_ThreeSharers_Returns3_34_Each()
    {
        var (pA, pB, pC) = CreateSortedParticipants();
        var shares = EqualSplitCalculator.Calculate(10.02m, new[] { pA, pB, pC });

        Assert.All(shares, s => Assert.Equal(3.34m, s.Amount));
        Assert.Equal(10.02m, shares.Sum(s => s.Amount));
    }

    [Fact(DisplayName = "TEST E: Input sharer order independence")]
    public void Calculate_InputOrderIndependence_ProducesIdenticalParticipantShareMapping()
    {
        var (pA, pB, pC) = CreateSortedParticipants();

        var result1 = EqualSplitCalculator.Calculate(100.00m, new[] { pA, pB, pC });
        var result2 = EqualSplitCalculator.Calculate(100.00m, new[] { pC, pA, pB });
        var result3 = EqualSplitCalculator.Calculate(100.00m, new[] { pB, pC, pA });

        var map1 = result1.ToDictionary(s => s.ParticipantId, s => s.Amount);
        var map2 = result2.ToDictionary(s => s.ParticipantId, s => s.Amount);
        var map3 = result3.ToDictionary(s => s.ParticipantId, s => s.Amount);

        Assert.Equal(map1[pA], map2[pA]);
        Assert.Equal(map1[pB], map2[pB]);
        Assert.Equal(map1[pC], map2[pC]);

        Assert.Equal(map1[pA], map3[pA]);
        Assert.Equal(map1[pB], map3[pB]);
        Assert.Equal(map1[pC], map3[pC]);
    }

    [Fact(DisplayName = "TEST F: Repeated calculations produce identical results")]
    public void Calculate_RepeatedCalculations_ProducesIdenticalOutputs()
    {
        var (pA, pB, pC) = CreateSortedParticipants();

        var baseline = EqualSplitCalculator.Calculate(999.99m, new[] { pA, pB, pC });

        for (int i = 0; i < 1000; i++)
        {
            var current = EqualSplitCalculator.Calculate(999.99m, new[] { pA, pB, pC });
            Assert.Equal(baseline.Count, current.Count);
            for (int j = 0; j < baseline.Count; j++)
            {
                Assert.Equal(baseline[j].ParticipantId, current[j].ParticipantId);
                Assert.Equal(baseline[j].Amount, current[j].Amount);
            }
        }
    }

    [Fact(DisplayName = "One Sharer: receives total line-item amount")]
    public void Calculate_OneSharer_ReceivesFullAmount()
    {
        var (pA, _, _) = CreateSortedParticipants();
        var shares = EqualSplitCalculator.Calculate(100.00m, new[] { pA });

        Assert.Single(shares);
        Assert.Equal(pA, shares[0].ParticipantId);
        Assert.Equal(100.00m, shares[0].Amount);
    }

    [Fact(DisplayName = "Two Sharers Exact Division: 10.00 produces 5.00 each")]
    public void Calculate_TwoSharersExact_ReturnsEqualShares()
    {
        var (pA, pB, _) = CreateSortedParticipants();
        var shares = EqualSplitCalculator.Calculate(10.00m, new[] { pA, pB });

        Assert.Equal(2, shares.Count);
        Assert.All(shares, s => Assert.Equal(5.00m, s.Amount));
    }

    [Fact(DisplayName = "Zero or Negative Amount: Throws ArgumentOutOfRangeException")]
    public void Calculate_ZeroOrNegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var (pA, _, _) = CreateSortedParticipants();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EqualSplitCalculator.Calculate(0m, new[] { pA }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EqualSplitCalculator.Calculate(-10.00m, new[] { pA }));
    }

    [Fact(DisplayName = "Empty Sharers: Throws ArgumentException")]
    public void Calculate_EmptySharers_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            EqualSplitCalculator.Calculate(100.00m, Array.Empty<ParticipantId>()));
    }

    [Fact(DisplayName = "Duplicate Sharer: Throws ArgumentException")]
    public void Calculate_DuplicateSharers_ThrowsArgumentException()
    {
        var (pA, _, _) = CreateSortedParticipants();

        Assert.Throws<ArgumentException>(() =>
            EqualSplitCalculator.Calculate(100.00m, new[] { pA, pA }));
    }

    [Fact(DisplayName = "High-Precision Amount (>2 decimal places): Throws ArgumentException")]
    public void Calculate_MoreThanTwoDecimalPlaces_ThrowsArgumentException()
    {
        var (pA, _, _) = CreateSortedParticipants();

        Assert.Throws<ArgumentException>(() =>
            EqualSplitCalculator.Calculate(10.005m, new[] { pA }));
    }

    [Fact(DisplayName = "Property Matrix Test: Money conservation & fairness across arbitrary amounts and sharer counts")]
    public void Calculate_MatrixOfAmountsAndSharerCounts_SatisfiesAllInvariants()
    {
        decimal[] testAmounts = { 0.01m, 0.02m, 0.03m, 0.05m, 1.00m, 10.00m, 10.01m, 10.02m, 99.99m, 100.00m, 999.99m, 12345.67m };
        int[] sharerCounts = { 1, 2, 3, 4, 5, 7, 10, 13, 25, 50 };

        foreach (var amount in testAmounts)
        {
            foreach (var count in sharerCounts)
            {
                var sharers = Enumerable.Range(1, count)
                    .Select(i => new ParticipantId(Guid.NewGuid()))
                    .ToList();

                var shares = EqualSplitCalculator.Calculate(amount, sharers);

                Assert.Equal(count, shares.Count);

                // Invariant 1: Money Conservation (SUM == amount)
                var sum = shares.Sum(s => s.Amount);
                Assert.Equal(amount, sum);

                // Invariant 2: Non-negative shares
                Assert.All(shares, s => Assert.True(s.Amount >= 0m));

                // Invariant 3: Fairness (max - min <= 0.01m)
                var max = shares.Max(s => s.Amount);
                var min = shares.Min(s => s.Amount);
                Assert.True(max - min <= 0.01m, $"Failed for amount {amount} and sharers {count}: max={max}, min={min}");
            }
        }
    }
}
