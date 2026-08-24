# Handoff — Milestone 1.7 Complete

## Current State

Milestone 1.7 (Bill & Participant Domain Foundation) is complete.

## Capabilities Implemented

- `Bill` aggregate (`Owezy.Domain.Billing`):
  - Invariants: Non-empty title, valid `BillId`, `SplitterPhoneNumber`.
  - Splitter automatically added as initial participant.
  - `AddParticipant`: prevents duplicate participant phone numbers within a bill.
- `IBillService` / `BillService` (`Owezy.Application.Billing`):
  - `CreateBillAsync`: Splitter identity enforced from authentication context.
  - `AddParticipantAsync`: Enforces that caller must be a member of the bill.
- Persistence (`Owezy.Infrastructure.Persistence`):
  - Tables `Bills` and `BillParticipants`.
  - Unique index `(BillId, PhoneNumber)` on `BillParticipants` table.
  - Migration `AddBillAndParticipantTables` applied.
- API (`Owezy.Api.Billing`):
  - `POST /bills` (protected by JWT, reads splitter identity from token `sub` / `phone_number` claim).
  - `POST /bills/{billId}/participants` (protected by JWT, caller must be member).

## Verification

| Check | Result |
|---|---|
| Build | PASS |
| Unit tests | 69/69 |
| Architecture tests | 4/4 |
| Integration & API tests | 15/15 |
| Working tree | CLEAN (after commit) |

## Security & Scope Boundary

- Splitter identity MUST come from JWT authentication token (client body cannot specify splitter).
- Database enforces unique participant constraint per bill.
- No item, payment, OCR, or settlement tables exist.

## Next

Milestone 1.8. Do not implement until explicitly instructed.
