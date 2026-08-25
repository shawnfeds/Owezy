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

## Authentication, Bill Domain & Participant Access

OTP-based + JWT Access Token authentication.

Bill, Participant, Items, Calculation, Lifecycle & Participant Access Domain:
- `Bill` aggregate: `Id`, `Title`, `SplitterPhoneNumber`, `CreatedAt`, `Status` (`Active` / `Finalized`), `FinalizedAt`, `Participants`, `Items`, `AccessLinks`
- Splitter automatically added as initial participant
- Duplicate participant phone numbers rejected within a bill
- `BillItem`: `Id`, `BillId`, `Description`, `Quantity` (>0), `Amount` (>0 exact decimal total line-item amount), `SharerParticipantIds`
- `EqualSplitCalculator`: Pure domain service implementing equal-share division with largest-remainder rounding and deterministic tie-breaking by `ParticipantId ASC`
- Calculated shares are derived and NOT persisted
- Bill Lifecycle (`OPEN` -> `FINALIZED`):
  - An OPEN bill can receive new participants and items.
  - Finalization requires at least one participant and at least one item.
  - A FINALIZED bill is permanently immutable: adding participants/items returns 409 Conflict.
- Participant Access & Sharing:
  - Participant links available ONLY after a bill is FINALIZED.
  - Generated using 256-bit cryptographically random opaque tokens.
  - Raw tokens are NEVER persisted; only SHA-256 hashes (`TokenHash`) are stored in `ParticipantAccessLinks` table.
  - Participant view (`GET /participant-access/{token}`) is strictly participant-scoped (shows only that participant's items, total owed, and bill overview; no cross-participant info or payment tracking).
- Database persistence: `Bills`, `BillParticipants`, `BillItems`, `BillItemSharers`, and `ParticipantAccessLinks` tables
- Endpoints:
  - `POST /auth/otp/request` → `202 Accepted`
  - `POST /auth/otp/verify` → `200 OK` (`accessToken`)
  - `POST /bills` → `201 Created` (Requires JWT auth, uses token `sub` for splitter identity)
  - `POST /bills/{billId}/participants` → `200 OK` (Requires JWT auth, caller must be bill member)
  - `POST /bills/{billId}/items` → `201 Created` (Requires JWT auth, caller must be authenticated splitter)
  - `POST /bills/{billId}/finalize` → `200 OK` (Requires JWT auth, caller must be authenticated splitter)
  - `POST /bills/{billId}/participants/{participantId}/access-link` → `200 OK` (Requires JWT auth, caller must be authenticated splitter)
  - `GET /participant-access/{token}` → `200 OK` (AllowAnonymous, token credential)

## Persistence

**Tables**:
- `OtpChallenges`
- `Bills`
- `BillParticipants`
- `BillItems`
- `BillItemSharers`
- `ParticipantAccessLinks`

- SQL Server + EF Core. Repositories in `Infrastructure`.
- Application contracts: `IOtpChallengeRepository`, `IBillRepository`
- Implementations: `SqlOtpChallengeRepository`, `SqlBillRepository`

## Completed Milestones

- **1.1** Authentication Domain Foundation — COMPLETE
- **1.2** OTP Domain & Service Contracts — COMPLETE
- **1.3** OTP SQL Server Persistence — COMPLETE
- **1.4** OTP Authentication Workflow — COMPLETE
- **1.5** Authentication API Boundary — COMPLETE
- **1.6** Access Token Issuance — COMPLETE
- **1.7** Bill & Participant Domain Foundation — COMPLETE
- **1.8** Bill Items & Sharer Definitions — COMPLETE
- **1.9** Authoritative Split Calculation Engine — COMPLETE
- **2.0** Bill Lifecycle & Finalization — COMPLETE
- **2.0.1** Finalization Participant Invariant Fix — COMPLETE
- **Participant Access & Sharing** — COMPLETE

## Current Milestone

Next Phase — see `CURRENT_TASK.md` for objective.

## Not Yet Implemented

OCR pipeline, payment tracking, UPI link generation, settlement, notifications, QR codes.

Do not expand into these areas unless explicitly instructed.
