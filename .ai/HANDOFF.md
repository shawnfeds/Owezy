# Handoff — Milestone 1.8 Complete

## Current State

Milestone 1.8 (Bill Items & Sharer Definitions) is complete.

## Capabilities Implemented

- `BillItem` entity & `Bill` aggregate updates (`Owezy.Domain.Billing`):
  - Invariants: Non-empty description, positive `Quantity` (>0), positive `Amount` (>0 decimal total line-item amount), one or more unique sharer `ParticipantId`s.
  - Cross-bill participant IDs rejected when defining item sharers.
  - Duplicate sharer IDs within an item rejected.
  - Splitter can be included as an item sharer.
- `IBillService` / `BillService` (`Owezy.Application.Billing`):
  - `AddBillItemAsync`: Only authenticated splitter can add items to the bill.
- Persistence (`Owezy.Infrastructure.Persistence`):
  - Tables `BillItems` and `BillItemSharers`.
  - Migration `AddBillItemsAndSharersTables` generated.
  - Relationship `Bill` 1:N `BillItems`, `BillItem` M:N `BillParticipants` via `BillItemSharers`.
- API (`Owezy.Api.Billing`):
  - `POST /bills/{billId}/items` (protected by JWT, only authenticated splitter can add items).

## Verification

| Check | Result |
|---|---|
| Build | PASS |
| Unit tests | 80/80 |
| Architecture tests | 4/4 |
| Integration & API tests | 27/27 |
| Working tree | CLEAN (after commit) |

## Security & Scope Boundary

- Only authenticated splitter can add items to a bill.
- Cross-bill participant IDs cannot escape bill boundaries.
- No per-person split calculation, Largest Remainder algorithm, or rounding implemented yet.
- No OCR, receipt storage, payment tracking, settlement, or sharing links exist.

## Next

Milestone 1.9 (Largest Remainder Calculation Engine). Do not implement until explicitly instructed.
