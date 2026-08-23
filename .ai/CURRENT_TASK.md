# Current Active Task — Milestone 0.2: Foundation Refinement & Baseline Locking

## Objective
Lock and correct the engineering foundation before feature development. Refine technology stack references (.NET 10, `Owezy.slnx`), strengthen directional architecture tests, update participant security terminology, clarify OCR/OTP abstractions, and correct phase plan definitions.

## Active Scope
- [x] Standardize .NET version (.NET 10) and solution file (`Owezy.slnx`) across docs
- [x] Update Phase 0 definition to accurately reflect current repository state
- [x] Strengthen directional architecture tests (`Domain` $\leftarrow$ `Application` $\leftarrow$ `Infrastructure` $\leftarrow$ `Api`)
- [x] Update participant security terminology to "Scoped read access + limited payment-status mutation"
- [x] Clarify `billToken` + `participantToken` relationship scoping
- [x] Remove premature SMS provider commitments (abstract `DevelopmentSmsProvider` + `ProductionSmsProvider`)
- [x] Update OCR resilience requirements (remove Polly dependency, specify architecture requirements)
- [x] Correct phase plan frontend strategy (feature-specific minimum UI in Phases 1–6; Phase 7 redefined to "UI Consolidation, PWA & UX Hardening")
- [x] Verify solution builds cleanly and architecture tests pass
- [x] Git check & commit (`chore: establish owezy engineering baseline`)

## Non-Goals
- ALL application feature implementation (No Auth, OTP, Bills, OCR, Splitting, Links, Payments, UI components).
