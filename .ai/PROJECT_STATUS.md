# Owezy — Project Status

## Project

Owezy — lightweight bill-splitting application.

## Architecture

Modular monolith. Single solution: `Owezy.slnx`.

Dependency direction:
```
Domain ← Application ← Infrastructure ← API
```

- `Domain`: zero external dependencies.
- `Application`: depends only on `Domain`. Must not depend on `Infrastructure`.
- `Infrastructure`: depends on `Application` + `Domain`. Must not depend on `API`.
- `API`: depends on `Application` and `Infrastructure` (composition root only).

Architecture enforced by NetArchTest in `Owezy.ArchitectureTests`.

## Technology

- .NET 10 / C# / ASP.NET Core
- Entity Framework Core + SQL Server
- Tesseract OCR (local, via `Tesseract` NuGet)

## Capabilities

OTP-based + JWT Access Token authentication.

Full billing lifecycle: create → add participants → add items → assign sharers → finalize → participant access → payment → settlement.

- `Bill` aggregate: `Id`, `Title`, `SplitterPhoneNumber`, `CreatedAt`, `Status` (`Active`/`Finalized`), `FinalizedAt`, `Participants`, `Items`, `AccessLinks`
- `EqualSplitCalculator`: largest-remainder rounding, deterministic by `ParticipantId ASC`. Shares are derived, NOT persisted.
- Bill Lifecycle (`OPEN` → `FINALIZED`): requires at least 1 participant, at least 1 item, and EVERY item must have at least 1 sharer.
- Participant Access: finalized-only, 256-bit opaque tokens, SHA-256 hash stored. Raw token never persisted.
- Payment Tracking: self-reported `Unpaid/Paid` status on `BillParticipant`. Server-timestamped `PaidAt`. Idempotent.
- Settlement: read-only derived calculation (TotalOwed, TotalPaid, TotalRemaining). Splitter-visible only. No DB changes.
- Receipt Capture & OCR: upload → OCR draft → splitter review/correction → explicit confirmation → BillItems.
- Sharer Assignment: `PUT /bills/{billId}/items/{itemId}/sharers` replaces the full sharer set atomically.
- Startup safety: missing/short JWT key **fails startup deterministically** with a clear error message.

## Endpoints

- `GET  /health` → `200 OK` (Healthy)
- `POST /auth/otp/request` → `202 Accepted`
- `POST /auth/otp/verify` → `200 OK` (`accessToken`)
- `POST /bills` → `201 Created` (JWT auth)
- `POST /bills/{billId}/participants` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/items` → `201 Created` (JWT auth, splitter)
- `PUT  /bills/{billId}/items/{itemId}/sharers` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/finalize` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/participants/{participantId}/access-link` → `200 OK` (JWT auth, splitter)
- `GET  /bills/{billId}/payments` → `200 OK` (JWT auth, splitter)
- `GET  /bills/{billId}/settlement` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/receipt` → `201 Created` (JWT auth, splitter, image upload)
- `GET  /bills/{billId}/receipt/{receiptId}` → `200 OK` (JWT auth, splitter)
- `PUT  /bills/{billId}/receipt/{receiptId}` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/receipt/{receiptId}/confirm` → `200 OK` (JWT auth, splitter)
- `GET  /participant-access/{token}` → `200 OK` (AllowAnonymous)
- `POST /participant-access/{token}/payment` → `200 OK` (AllowAnonymous)

## Persistence

**Tables**: `OtpChallenges`, `Bills`, `BillParticipants`, `BillItems`, `BillItemSharers`, `ParticipantAccessLinks`, `Receipts`

## Completed Milestones

- **1.1–2.0.1** Auth + Bill/Participant/Items/Finalization foundation — COMPLETE
- **Participant Access & Sharing** — COMPLETE
- **Payment Tracking** — COMPLETE
- **Settlement & Final Balance** — COMPLETE
- **Receipt Capture & OCR Foundation** — COMPLETE
- **OCR Review & Confirmation** — COMPLETE
- **Sharer Assignment & Final Bill Composition** — COMPLETE
- **End-to-End Billing Consistency Audit & Hardening** — COMPLETE
- **API Contract & Error-Handling Hardening** — COMPLETE
- **MVP Production-Safety Audit** — COMPLETE
- **MVP Operational Readiness & Deployment Foundation** — COMPLETE
- **Final MVP Architecture & Readiness Audit** — COMPLETE

## Not Yet Implemented

UPI link generation, debt simplification, notifications, QR codes, payment gateways.
