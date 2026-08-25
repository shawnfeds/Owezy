# Handoff — Payment Tracking Complete

## Current State

Payment Tracking milestone is complete.

## Capabilities Implemented

- Domain Layer (`Owezy.Domain.Billing`):
  - `PaymentStatus` (`Unpaid = 1`, `Paid = 2`) enum.
  - `Participant` updated with `PaymentStatus`, `PaidAt`, and `MarkPaid(now)` method (idempotent, server timestamped).
  - `Bill.MarkParticipantPaid(participantId, now)`: Finalized bill requirement enforced; participant membership validated.
- Application Layer (`Owezy.Application.Billing`):
  - `MarkParticipantPaidByTokenAsync`: Allows participant to mark self paid using opaque access token.
  - `GetSplitterBillPaymentsAsync`: Authenticated splitter-only operation retrieving group payment status and derived amounts owed.
  - Updated `ParticipantBillViewResult` to include participant's own payment status and `PaidAt`.
- Infrastructure Layer (`Owezy.Infrastructure`):
  - `BillParticipantRow` updated with `PaymentStatus` (default `1`) and `PaidAt`.
  - `BillParticipantConfiguration` updated with EF Core properties.
  - Mappings updated in `SqlBillRepository`.
  - EF Core migration `AddParticipantPaymentStatus`.
- API Layer (`Owezy.Api.Billing`):
  - `POST /participant-access/{token}/payment`: Token credential endpoint (`200 OK` or `404 Not Found`).
  - `GET /bills/{billId}/payments`: Splitter-only endpoint (`200 OK`, `401 Unauthorized`, `403 Forbidden`, `409 Conflict` if OPEN).
  - Updated `GET /participant-access/{token}` response payload with `paymentStatus` and `paidAt`.

## Verification

| Check | Result |
|---|---|
| Build | PASS (0 warnings, 0 errors) |
| Unit tests | 135/135 PASS |
| Architecture tests | 4/4 PASS |
| Integration & API tests | 47/47 PASS |
| Total test suite | 186/186 PASS |
| Working tree | CLEAN (after commit) |

## Security & Scope Boundary

- Payment is self-reported status tracking only (no payment processing, gateways, or settlement).
- Participant can mark ONLY themselves paid via token.
- Participant sees ONLY their own status in the participant view.
- Splitter sees group payment status for their own finalized bill only.
- OPEN bills reject all payment tracking operations.
- Payment amounts continue to be derived from `EqualSplitCalculator`.

## Next

Wait for next explicit instruction.
