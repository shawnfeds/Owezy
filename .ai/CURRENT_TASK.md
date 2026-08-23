# Current Active Task — Phase 1 / Milestone 1.3: OTP Persistence Foundation

## Objective
Establish SQL Server EF Core persistence for OTP challenges.

## Status
**COMPLETED**. Next milestone: Phase 1 / Milestone 1.4 — Authentication Flow Orchestration.

## What was implemented
- [x] `OwezyDbContext` with `DbSet<OtpChallengeRow>` (Infrastructure only)
- [x] `OtpChallengeRow` — EF persistence model (internal to Infrastructure)
- [x] `OtpChallengeConfiguration` — Fluent API mapping with all columns, constraints, index, and rowversion
- [x] `OwezyDbContextFactory` — Design-time factory for `dotnet ef migrations add`
- [x] `SqlOtpChallengeRepository` — `IOtpChallengeRepository` implementation
- [x] `InfrastructureAssemblyMarker` — Assembly marker (replaced deleted `Class1.cs`)
- [x] Migration: `InitialOtpChallengeSchema` created and verified
- [x] SQL Server integration tests (8 tests — all passing against LocalDB)
- [x] Architecture tests updated and passing (4/4)
- [x] Unit tests unchanged and passing (49/49)
