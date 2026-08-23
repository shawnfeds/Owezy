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
- `API`: depends on all layers.

## Technology

- .NET 10 / C# / ASP.NET Core
- Entity Framework Core
- SQL Server

## Authentication

OTP-based. Phone → OTP → JWT (JWT not yet implemented).

Current OTP security & application workflow:
- Cryptographically secure 6-digit OTP
- 5-minute expiration
- Max 5 failed attempts
- HMAC-SHA-256 verifier with external HMAC secret
- Constant-time verification
- Plaintext OTP never persisted or returned
- `RequestOtpAsync` (creates challenge, hashes OTP, persists, sends via `ISmsProvider`, handles SMS failure)
- `VerifyOtpAsync` (validates OTP, handles attempts/expiry/exhaustion, returns `VerifyOtpResult` with canonical phone identity)

## Persistence

**Table**: `OtpChallenges`

Key fields: `Id`, `PhoneNumber`, `OtpHash`, `CreatedAt`, `ExpiresAt`, `RemainingAttempts`, `State`, `RowVersion`

- SQL Server + EF Core. Repository in `Infrastructure`.
- Application contract: `IOtpChallengeRepository`
- Implementation: `SqlOtpChallengeRepository`
- Concurrency: `rowversion` optimistic concurrency token.

## Completed Milestones

- **1.1** Authentication Domain Foundation — COMPLETE
- **1.2** OTP Domain & Service Contracts — COMPLETE
- **1.3** OTP SQL Server Persistence — COMPLETE
- **1.4** OTP Authentication Workflow — COMPLETE

## Current Milestone

**1.5** — see `CURRENT_TASK.md` for objective.

## Not Yet Implemented

JWT, authentication API endpoints, production SMS, refresh tokens, authorization, background cleanup, Redis, participant/bill/payment functionality.

Do not expand into these areas unless explicitly instructed.
