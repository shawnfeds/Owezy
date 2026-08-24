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

## Authentication & Bill Domain

OTP-based + JWT Access Token authentication.

Bill & Participant Domain Foundation:
- `Bill` aggregate: `Id`, `Title`, `SplitterPhoneNumber`, `CreatedAt`, `Status`, `Participants`
- Splitter automatically added as initial participant
- Duplicate participant phone numbers rejected within a bill
- Database persistence: `Bills` and `BillParticipants` tables with unique index on `(BillId, PhoneNumber)`
- Endpoints:
  - `POST /auth/otp/request` → `202 Accepted`
  - `POST /auth/otp/verify` → `200 OK` (`accessToken`)
  - `POST /bills` → `201 Created` (Requires JWT auth, uses token `sub` for splitter identity)
  - `POST /bills/{billId}/participants` → `200 OK` (Requires JWT auth, caller must be bill member)

## Persistence

**Tables**:
- `OtpChallenges`
- `Bills`
- `BillParticipants`

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

## Current Milestone

**1.8** — see `CURRENT_TASK.md` for objective.

## Not Yet Implemented

Bill items, item splitting, Largest Remainder Method, OCR pipeline, participant link access tokens, payment tracking, UPI link generation, settlement.

Do not expand into these areas unless explicitly instructed.
