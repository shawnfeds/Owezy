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

## Technology

- .NET 10 / C# / ASP.NET Core
- Entity Framework Core
- SQL Server

## Capabilities

OTP-based + JWT Access Token authentication.

Bill, Participant, Items, Calculation, Lifecycle, Participant Access, Payment Tracking, Settlement, Receipt Capture & OCR Review/Confirmation:
- `Bill` aggregate: `Id`, `Title`, `SplitterPhoneNumber`, `CreatedAt`, `Status` (`Active`/`Finalized`), `FinalizedAt`, `Participants`, `Items`, `AccessLinks`
- `EqualSplitCalculator`: largest-remainder rounding, deterministic by `ParticipantId ASC`. Shares are derived, NOT persisted.
- Bill Lifecycle (`OPEN` → `FINALIZED`): at least 1 participant + 1 item required.
- Participant Access: finalized-only, 256-bit opaque tokens, SHA-256 hash stored.
- Payment Tracking: self-reported `Unpaid/Paid` status on `BillParticipant`. Server-timestamped `PaidAt`. Idempotent mark-paid.
- Settlement: read-only derived calculation (TotalOwed, TotalPaid, TotalRemaining, per-participant state). Splitter-visible only. No DB changes.
- Receipt Capture & OCR Foundation:
  - `Receipt` aggregate: `Id`, `BillId`, `StorageKey`, `Status` (`Created`/`Processed`/`Failed`/`Confirmed`), `CreatedAt`, `ConfirmedAt`, `OcrDraft`.
  - `IOcrService` abstraction wrapping local/free Tesseract OCR.
  - `IReceiptStorage` abstraction storing files on local filesystem using server-generated GUID keys.
- OCR Review & Confirmation:
  - Splitter can review and edit/correct OCR drafts while bill is OPEN (`PUT /bills/{billId}/receipt/{receiptId}`).
  - Explicit confirmation (`POST /bills/{billId}/receipt/{receiptId}/confirm`) converts validated OCR line items into actual `BillItems` on the `Bill` aggregate.
  - OCR line items created with NO auto-assigned sharers (sharer assignment is done separately by splitter).
  - Confirmation is idempotent / prevents duplicate `BillItems`.
  - Finalized bills cannot be modified through OCR confirmation.

## Endpoints

- `POST /auth/otp/request` → `202 Accepted`
- `POST /auth/otp/verify` → `200 OK` (`accessToken`)
- `POST /bills` → `201 Created` (JWT auth)
- `POST /bills/{billId}/participants` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/items` → `201 Created` (JWT auth, splitter)
- `POST /bills/{billId}/finalize` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/participants/{participantId}/access-link` → `200 OK` (JWT auth, splitter)
- `GET  /bills/{billId}/payments` → `200 OK` (JWT auth, splitter)
- `GET  /bills/{billId}/settlement` → `200 OK` (JWT auth, splitter)
- `POST /bills/{billId}/receipt` → `201 Created` (JWT auth, splitter, image upload)
- `GET  /bills/{billId}/receipt/{receiptId}` → `200 OK` (JWT auth, splitter)
- `PUT  /bills/{billId}/receipt/{receiptId}` → `200 OK` (JWT auth, splitter, edit OCR draft)
- `POST /bills/{billId}/receipt/{receiptId}/confirm` → `200 OK` (JWT auth, splitter, creates BillItems)
- `GET  /participant-access/{token}` → `200 OK` (AllowAnonymous)
- `POST /participant-access/{token}/payment` → `200 OK` (AllowAnonymous)

## Persistence

**Tables**: `OtpChallenges`, `Bills`, `BillParticipants`, `BillItems`, `BillItemSharers`, `ParticipantAccessLinks`, `Receipts`

- `Receipts` columns: `Id`, `BillId`, `StorageKey`, `Status`, `CreatedAt`, `ConfirmedAt`, `OcrResultJson`. No image binary in SQL.

## Completed Milestones

- **1.1–1.6** Authentication — COMPLETE
- **1.7** Bill & Participant Domain Foundation — COMPLETE
- **1.8** Bill Items & Sharer Definitions — COMPLETE
- **1.9** Authoritative Split Calculation Engine — COMPLETE
- **2.0** Bill Lifecycle & Finalization — COMPLETE
- **2.0.1** Finalization Participant Invariant Fix — COMPLETE
- **Participant Access & Sharing** — COMPLETE
- **Payment Tracking** — COMPLETE
- **Settlement & Final Balance** — COMPLETE
- **Receipt Capture & OCR Foundation** — COMPLETE
- **OCR Review & Confirmation** — COMPLETE

## Not Yet Implemented

UPI link generation, debt simplification, notifications, QR codes, payment gateways.
