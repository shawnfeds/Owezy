# Owezy Phase & Milestone Implementation Plan

This document outlines the detailed 10-phase roadmap for building Owezy. Every phase represents a controlled milestone with strict scope enforcement, test requirements, and completion gates.

> [!IMPORTANT]
> **INCREMENTAL FRONTEND STRATEGY**
> Each feature phase (Phases 1–6) includes the **minimum UI required** to implement and verify that feature end-to-end. Phase 7 is reserved for UI consolidation, PWA capabilities, and final UX polish.

---

## Phase 0 — Foundation & Infrastructure Scaffolding

### Objective
Establish solution directory structure (`Owezy.slnx`), project files, AI governance files, ADR suite, technical specifications, and architecture testing scaffolding.

### Actual Scope Delivered
- .NET 10 Solution (`Owezy.slnx`) setup with modular monolith layer layout (`Owezy.Api`, `Owezy.Application`, `Owezy.Domain`, `Owezy.Infrastructure`, `Owezy.Client`).
- `.ai/` engineering governance framework (`AGENT_RULES.md`, `PROJECT_STATUS.md`, `CURRENT_TASK.md`, `HANDOFF.md`).
- 10 Architecture Decision Records (`docs/adr/ADR-001.md` through `ADR-010.md`).
- Canonical Technical Specifications (`docs/spec/`).
- Architecture test suite asserting directional dependency rules (`Domain` $\leftarrow$ `Application` $\leftarrow$ `Infrastructure` $\leftarrow$ `Api`).

### Expected Scaffolding Files
- `Owezy.slnx`
- `src/Owezy.Api/Owezy.Api.csproj`, `Program.cs`
- `src/Owezy.Application/Owezy.Application.csproj`
- `src/Owezy.Domain/Owezy.Domain.csproj`
- `src/Owezy.Infrastructure/Owezy.Infrastructure.csproj`
- `src/Owezy.Client/index.html`, `styles/main.css`, `js/app.js`
- `tests/Owezy.UnitTests/Owezy.UnitTests.csproj`
- `tests/Owezy.IntegrationTests/Owezy.IntegrationTests.csproj`
- `tests/Owezy.ArchitectureTests/LayerDependencyTests.cs`

### Acceptance Criteria
- Solution compiles cleanly with zero errors (`dotnet build`).
- `Owezy.ArchitectureTests` runs and verifies that `Domain` has no dependencies on Application/Infrastructure/Api, `Application` has no dependency on Infrastructure/Api, and `Infrastructure` has no dependency on Api.

### Completion Gate
- Clean build, passing architecture tests, git repository initialized with baseline commit.

---

## Phase 1 — Splitter Authentication

### Objective
Implement Phone + OTP + JWT authentication for Splitters with `DevelopmentSmsProvider` and minimum authentication UI.

### Scope
- Backend: Phone normalization, OTP generation, SHA-256 hashing, verification (`RequestOtpCommand`, `VerifyOtpCommand`), `DevelopmentSmsProvider`, `IJwtTokenGenerator`, endpoints (`/api/auth/request-otp`, `/api/auth/verify-otp`).
- Minimum UI: Phone input form, OTP entry widget, and JWT token storage in `Owezy.Client`.

### Dependencies
- Phase 0.

### Expected Files
- `Owezy.Application/Auth/...`
- `Owezy.Infrastructure/Auth/...`
- `Owezy.Api/Controllers/AuthController.cs`
- `src/Owezy.Client/js/auth.js`

### Acceptance Criteria
- Valid phone number receives HTTP 200 on OTP request (logged to dev console).
- Valid 6-digit OTP returns JWT token and logs user in UI.
- Invalid OTP fails with HTTP 400 after 3 attempts.

### Tests Required
- Unit tests for phone normalization, OTP hashing, attempt counting.
- Integration tests for auth API controller.

### Non-Goals
- Production SMS provider integration.

### Completion Gate
- Unit & integration tests passing 100%.

---

## Phase 2 — Bill Management Core

### Objective
Allow authenticated Splitters to create bills, add/edit line items, add participants, and review bill state with minimum bill management UI.

### Scope
- Backend: Entities (`Bill`, `BillItem`, `Participant`), use cases (`CreateBillCommand`, `AddBillItemCommand`, `UpdateBillItemCommand`, `AddParticipantCommand`, `GetSplitterBillQuery`), API endpoints (`/api/bills`).
- Minimum UI: Bill creation form, item addition list, participant input widget.

### Dependencies
- Phase 1 (Splitter JWT required).

### Acceptance Criteria
- Authenticated Splitter can create bill with title, date, service charge/tax.
- Items added with `Quantity`, `UnitPrice`, `LineTotal`.
- Participants added by name.

### Tests Required
- Entity domain validation unit tests.
- Bill CRUD integration tests.

### Completion Gate
- All bill management endpoints and minimum UI verified.

---

## Phase 3 — Advisory OCR Pipeline

### Objective
Implement `IOcrService` with image SHA-256 hashing, SQL caching, external OCR provider abstraction, and OCR review UI.

### Scope
- Backend: `IOcrService` interface, image SHA-256 hash calculation and cache repository (`OcrCacheEntry`), external OCR provider, upload endpoint (`POST /api/bills/ocr-scan`), rate limiting middleware.
- Minimum UI: Receipt image upload button, scan preview widget, line item review/editing screen.

### Dependencies
- Phase 2.

### Acceptance Criteria
- Scanning receipt image extracts candidate items (`Name`, `Quantity`, `UnitPrice`, `LineTotal`).
- Re-submitting identical image hash returns cached JSON result without calling external OCR API.
- User can correct OCR output in review UI.

### Tests Required
- SHA-256 hash deduplication unit tests.
- OCR cache repository integration tests.

### Completion Gate
- OCR caching, review UI, and fallback logic verified.

---

## Phase 4 — Splitting Engine

### Objective
Implement equal item claiming, tax/service charge distribution, Largest Remainder Method rounding algorithm, and minimum claim/split UI.

### Scope
- Backend: Item claims (`ParticipantItemClaim`), `LargestRemainderSplitter` domain service, `FinalizeBillSplitCommand`, exact paisa reconciliation invariant.
- Minimum UI: Item claim checkboxes per participant, instant split summary view.

### Dependencies
- Phase 2.

### Acceptance Criteria
- Items claimed by $N$ participants divided equally.
- Sum of all participant shares equals `BillTotal` exactly.
- Fractional tie-breaking is 100% deterministic.

### Tests Required
- Comprehensive unit test matrix covering edge cases (e.g. ₹100 split 3 ways, ₹0.01 remainders, multiple items).

### Completion Gate
- 100% test pass rate on monetary calculation test suite.

---

## Phase 5 — Participant Sharing & Scoped Privacy

### Objective
Generate relationship-scoped participant links (`/split/{billToken}/{participantToken}`) with server-side scoped privacy and minimum participant split UI.

### Scope
- Backend: Cryptographic token pair generation, endpoint `GET /api/split/{billToken}/{participantToken}`, scoped query handler returning `ParticipantShareDto`.
- Minimum UI: Participant split view showing claimed items, calculated share total, payment status, and Splitter UPI VPA.

### Dependencies
- Phase 4.

### Acceptance Criteria
- Participant link resolves participant's individual share.
- Response contains ONLY that participant's items, total, and payment details (**"Scoped read access + limited payment-status mutation"**).
- Excludes other participants' financial totals or claimed items on the server.

### Tests Required
- Privacy isolation tests (verifying Alice cannot see Bob's data).
- Token security tests (verifying non-derivability).

### Completion Gate
- Security privacy isolation suite passing.

---

## Phase 6 — UPI Payments & Status Confirmation

### Objective
Generate UPI payment links (`upi://pay`), allow participants to mark split as paid, allow Splitter to confirm payment, and minimum payment UI.

### Scope
- Backend: `UpiPaymentUrlBuilder`, participant endpoint `POST /api/split/{billToken}/{participantToken}/mark-paid`, splitter endpoint `POST /api/bills/{billId}/participants/{participantId}/confirm-payment`.
- Minimum UI: "Pay via UPI" button launching UPI app, "Mark as Paid" button for participant, payment confirmation toggle for Splitter.

### Dependencies
- Phase 5.

### Acceptance Criteria
- `upi://pay` deep link correctly includes Splitter VPA, participant amount, and reference note.
- Status transition: `Unpaid` -> `MarkedPaid` -> `Confirmed`.

### Tests Required
- UPI string formatting unit tests.
- Payment state transition integration tests.

### Completion Gate
- Status state transitions verified via tests.

---

## Phase 7 — UI Consolidation, PWA & UX Hardening

### Objective
Refine responsive layout, implement PWA capabilities, polish user experience, ensure accessibility, and finalize visual presentation across all existing feature screens.

### Scope
- Responsive UI refinement (mobile, tablet, desktop viewports).
- PWA features: Service worker, web app manifest, offline indicator.
- UX polish: Micro-animations, loading states, error boundary banners, visual hierarchy.
- Installability & cross-screen integration testing.

### Dependencies
- Phases 1–6.

### Acceptance Criteria
- Fully responsive across 320px to 1200px viewports.
- Service worker and web manifest registered cleanly.
- Smooth visual presentation without UI flicker or state desynchronization.

### Completion Gate
- Visual and responsive UX verification clean run.

---

## Phase 8 — Security, Hardening & Rate Limiting

### Objective
Harden API security, enforce global rate limits, security headers, input sanitization, and architecture rules.

### Scope
- Global ASP.NET Core rate limiting middleware.
- Security header injection (CSP, HSTS, X-Frame-Options).
- Input validation filters.
- `Owezy.ArchitectureTests` suite execution.

### Dependencies
- Phases 1–7.

### Acceptance Criteria
- All endpoints protected by rate limiting.
- Security headers present in responses.
- Layer rules strictly enforced.

### Completion Gate
- Security scan and architecture test clean run.

---

## Phase 9 — End-to-End Verification

### Objective
Execute comprehensive E2E test suite verifying full bill-splitting lifecycle.

### Scope
- End-to-end automated test runner.
- Simulates Splitter creation -> OCR scan -> participant links -> payment confirmation.

### Completion Gate
- 100% green build on CI/CD test suite.

---

## Phase 10 — Post-v1 Architecture Review

### Objective
Perform post-implementation technical debt audit and final architectural review.

### Scope
- Review ADR compliance.
- Classify findings as Implemented, Deferred, or Requires Action.

### Completion Gate
- Final signoff report.
