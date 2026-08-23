# Owezy Splitting Engine & Monetary Calculation Specification

## 1. Monetary Precision Standard
All financial amounts must be represented using C# `decimal` (SQL `decimal(18,2)`). Floating-point math is strictly forbidden.

## 2. Calculation Pipeline

```text
1. Collect Items & Claimants
      ↓
2. Calculate Item Shares for each Participant
      ↓
3. Sum Item Shares + Proportional Taxes/Fees for each Participant
      ↓
4. Apply Largest Remainder Method (Hare-Niemeyer)
      ↓
5. Perform Deterministic Tie-Breaking (if needed)
      ↓
6. Output Final Reconciled Participant Totals
```

## 3. Mathematical Formulae

### A. Equal Item Split
For an item $i$ with line total $L_i$ claimed by a set of participants $P_i$ where $|P_i| = N_i$:
$$\text{Share}_{p, i} = \frac{L_i}{N_i}$$

### B. Unrounded Participant Total
For participant $p$, sum of item shares plus proportional tax/service charge ratio ($R_{\text{extra}}$):
$$\text{ExactTotal}_p = \left( \sum_{i \in \text{Claimed}_p} \text{Share}_{p, i} \right) \times (1 + R_{\text{extra}})$$

### C. Largest Remainder Method (Hare-Niemeyer Algorithm)
1. **Floor Shares**: For each participant $p$, compute floored share in minor units (paisa/cents):
   $$\text{BaseUnits}_p = \lfloor \text{ExactTotal}_p \times 100 \rfloor$$
2. **Fractional Remainder**:
   $$\text{Remainder}_p = (\text{ExactTotal}_p \times 100) - \text{BaseUnits}_p$$
3. **Calculate Leftover Units**:
   $$\text{TotalUnits} = \text{Round}(\text{BillTotal} \times 100)$$
   $$\text{LeftoverUnits} = \text{TotalUnits} - \sum_{p} \text{BaseUnits}_p$$
4. **Distribute Leftovers**: Order participants by $\text{Remainder}_p$ descending. Add 1 paisa ($0.01$) to the top $\text{LeftoverUnits}$ participants.
5. **Deterministic Tie-Breaking**: If $\text{Remainder}_a == \text{Remainder}_b$, order by `Participant.CreatedAt` ascending, then `Participant.Id` ascending.

## 4. Reconciliation Invariant
The engine enforces the core invariant:
$$\sum_{p \in \text{Participants}} \text{FinalTotal}_p == \text{BillTotal}$$

Unit tests in `Owezy.UnitTests` MUST assert this invariant across all test cases.
