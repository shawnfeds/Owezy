# Owezy Session Handoff Document

## Current State Summary
- Phase 1 / Milestone 1.3 (OTP Persistence Foundation) is **COMPLETED**.
- EF Core SQL Server infrastructure established inside `Owezy.Infrastructure`.
- All 62 tests pass (49 unit + 4 architecture + 8 SQL Server integration + 1 placeholder).

---

## Database Schema (Milestone 1.3)

### Table: `OtpChallenges`

| Column | SQL Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` | PK (not database-generated; domain supplies GUID) |
| `PhoneNumber` | `nvarchar(20)` | Canonical E.164 form only |
| `OtpHash` | `char(64)` | HMAC-SHA-256 hex verifier (fixed 64 ASCII chars) |
| `CreatedAt` | `datetimeoffset` | UTC timestamp |
| `ExpiresAt` | `datetimeoffset` | UTC timestamp |
| `RemainingAttempts` | `int` | Mutable |
| `State` | `int` | OtpState enum: 1=Active, 2=Verified, 3=Expired, 4=Exhausted |
| `RowVersion` | `rowversion` | EF optimistic concurrency token |

### Indexes
- **PK_OtpChallenges** on `Id` (clustered)
- **IX_OtpChallenges_PhoneNumber** on `PhoneNumber` (non-clustered) — supports active-challenge phone lookup in the authentication workflow

### Concurrency Strategy
SQL Server `rowversion` column mapped as EF Core concurrency token. Provides safe optimistic concurrency for state/attempt updates without requiring distributed locks or Redis.

---

## EF Core Decisions
- `OtpChallengeRow` is an Infrastructure-internal persistence model (never exposed through Application contracts).
- Domain `OtpChallenge` reconstructed from row data via `OtpChallenge.Reconstitute(...)`.
- Fluent API only — no EF Core attributes on domain entities.
- `PhoneNumber` stored as its canonical `.Value` string directly; reconstructed via `PhoneNumber.Create()`.
- `OtpState` stored as `int` with documented enum mapping.
- `OtpHash` stored as `char(64)` non-unicode (ASCII hex only).

---

## Repository Implementation
- `SqlOtpChallengeRepository` implements `IOtpChallengeRepository` (Application contract).
- Operations: `GetByIdAsync`, `AddAsync`, `UpdateAsync`.
- `UpdateAsync` mutates only `RemainingAttempts` and `State` — immutable fields are never modified.
- EF tracking used deliberately; IQueryable/DbContext/row types never returned to callers.

---

## Migration
- Name: `InitialOtpChallengeSchema`
- File: `src/Owezy.Infrastructure/Migrations/20260823152609_InitialOtpChallengeSchema.cs`
- Reversible (`Down` drops the table).

---

## Test Strategy
- SQL Server integration tests: `tests/Owezy.IntegrationTests/Auth/OtpChallengeRepositoryTests.cs`
- Tests connect to LocalDB: `(localdb)\mssqllocaldb;Database=Owezy_IntegrationTests`
- Tests call `MigrateAsync()` on startup and clean up after run.
- **All 8 SQL Server integration tests PASSED against real LocalDB.**
- Architecture tests enforcing layer boundaries: PASSING (4/4).

---

## Environment Requirements
- SQL Server LocalDB required for integration tests and local development.
- Connection string for production/staging: supply via `ConnectionStrings:OwezyDb` user-secret or environment variable. Value in `appsettings.json` is intentionally empty.

---

## Next Milestone
**Phase 1 / Milestone 1.4** — Authentication Flow Orchestration (OTP request/verify API endpoints + JWT issuance).

> [!WARNING]
> **HARD STOP**: Do NOT start Milestone 1.4 until explicitly instructed.
