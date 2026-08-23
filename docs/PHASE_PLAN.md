# Owezy Phase & Milestone Implementation Plan

This document outlines the detailed 10-phase roadmap for building Owezy. Every phase represents a controlled milestone with strict scope enforcement, test requirements, and completion gates.

---

## Phase 0 — Foundation & Infrastructure Scaffolding

### Objective
Establish solution directory structure, project files, EF Core DbContext, basic logging, health check endpoints, and architecture testing scaffolding.

### Scope
- .NET 8 Solution (`Owezy.sln`) setup with modular monolith layer layout.
- NuGet package references (`Microsoft.EntityFrameworkCore.SqlServer`, `NetArchTest.eNet`).
- Initial empty `OwezyDbContext` setup.
- Architecture test suite asserting dependency direction rules.

### Dependencies
- None.

### Expected Files
- `src/Owezy.Api/Owezy.Api.csproj`, `Program.cs`
- `src/Owezy.Application/Owezy.Application.csproj`
- `src/Owezy.Domain/Owezy.Domain.csproj`
- `src/Owezy.Infrastructure/Owezy.Infrastructure.csproj`
- `src/Owezy.Client/index.html`, `index.css`, `app.js`
- `tests/Owezy.UnitTests/Owezy.UnitTests.csproj`
- `tests/Owezy.IntegrationTests/Owezy.IntegrationTests.csproj`
- `tests/Owezy.ArchitectureTests/LayerDependencyTests.cs`

### Acceptance Criteria
- Solution compiles cleanly with zero warnings (`dotnet build`).
- `Owezy.ArchitectureTests` runs and verifies that `Owezy.Domain` has no dependencies on Infrastructure or Api.
- GET `/health` returns HTTP 200 OK.

### Tests Required
- Architecture unit tests for layer rules.

### Non-Goals
- No business logic, auth, or DB migrations executed yet.

### Completion Gate
- Clean build, passing architecture tests.

---

## Phase 1 — Splitter Authentication

### Objective
Implement Phone + OTP + JWT authentication for Splitters with `DevSmsProvider`.

### Scope
- Phone number normalization service.
- OTP generation, SHA-256 hashing, and verification use cases (`RequestOtpCommand`, `VerifyOtpCommand`).
- `IOtpService` and `ISmsProvider` implementations (`DevSmsProvider`).
- JWT token generator (`IJwtTokenGenerator`).
- Splitter authentication API endpoints (`/api/auth/request-otp`, `/api/auth/verify-otp`).

### Dependencies
- Phase 0.

### Expected Files
- `Owezy.Application/Auth/...`
- `Owezy.Infrastructure/Auth/...`
- `Owezy.Api/Controllers/AuthController.cs`

### Acceptance Criteria
- Valid phone number receives HTTP 200 on OTP request.
- Valid 6-digit OTP returns JWT token.
- Invalid OTP fails with HTTP 400 after 3 attempts.
- Expiration and resend cooldowns enforced.

### Tests Required
- Unit tests for phone normalization, OTP hashing, attempt counting.
- Integration tests for auth API controller.

### Non-Goals
- Production SMS provider setup.

### Completion Gate
- Unit & integration tests passing 100%.

---

## Phase 2 — Bill Management Core

### Objective
Allow authenticated Splitters to create bills, add/edit line items, add participants, and review bill state.

### Scope
- Entities: `Bill`, `BillItem`, `Participant`.
- Use cases: `CreateBillCommand`, `AddBillItemCommand`, `UpdateBillItemCommand`, `AddParticipantCommand`, `GetSplitterBillQuery`.
- API Endpoints: `/api/bills` (POST, GET, PUT).

### Dependencies
- Phase 1 (Splitter JWT required).

### Expected Files
- `Owezy.Domain/Entities/Bill.cs`, `BillItem.cs`, `Participant.cs`
- `Owezy.Application/Billing/...`
- `Owezy.Api/Controllers/BillsController.cs`

### Acceptance Criteria
- Authenticated Splitter can create bill with title, date, service charge/tax.
- Items added with `Quantity`, `UnitPrice`, `LineTotal`.
- Participants added by name.

### Tests Required
- Entity domain validation unit tests.
- Bill CRUD integration tests.

### Non-Goals
- OCR scan import, item splitting calculation, sharing links.

### Completion Gate
- All bill management endpoints verified via integration tests.

---

## Phase 3 — Advisory OCR Pipeline

### Objective
Implement `IOcrService` with image SHA-256 hashing, SQL caching, and Azure Document Intelligence provider abstraction.

### Scope
- `IOcrService` application interface.
- Image SHA-256 hash calculation and cache repository (`OcrCacheEntry`).
- Azure Document Intelligence provider (`AzureFormRecognizerOcrProvider`).
- Upload receipt endpoint (`POST /api/bills/ocr-scan`).
- Output parsed candidate items for Splitter review.

### Dependencies
- Phase 2.

### Expected Files
- `Owezy.Application/OCR/...`
- `Owezy.Infrastructure/OCR/...`
- `Owezy.Api/Controllers/OcrController.cs`

### Acceptance Criteria
- Scanning receipt image extracts line items (`Name`, `Quantity`, `UnitPrice`, `LineTotal`).
- Re-submitting identical image hash returns cached JSON result without calling Azure OCR.
- Rate limiting middleware blocks excessive OCR uploads (>5/hr).

### Tests Required
- SHA-256 hash deduplication unit tests.
- OCR cache repository integration tests.

### Non-Goals
- Redis cache.

### Completion Gate
- OCR caching and fallback logic verified.

---

## Phase 4 — Splitting Engine

### Objective
Implement equal item claiming, tax/service charge distribution, and the Largest Remainder Method rounding algorithm.

### Scope
- Item claim associations (`ParticipantItemClaim`).
- `LargestRemainderSplitter` domain service engine.
- Calculation use case (`FinalizeBillSplitCommand`).
- Exact paisa reconciliation invariants.

### Dependencies
- Phase 2.

### Expected Files
- `Owezy.Domain/Services/LargestRemainderSplitter.cs`
- `Owezy.Application/Splitting/...`

### Acceptance Criteria
- Items claimed by $N$ participants divided equally.
- Sum of all participant shares equals `BillTotal` exactly.
- Fractional tie-breaking is 100% deterministic.

### Tests Required
- Comprehensive unit test matrix covering edge cases (e.g. ₹100 split 3 ways, ₹0.01 remainders, multiple items).

### Non-Goals
- Per-person weighted quantity consumption tracking.

### Completion Gate
- 100% test pass rate on monetary calculation test suite.

---

## Phase 5 — Participant Sharing & Scoped Privacy

### Objective
Generate secure, non-derivable participant links and enforce backend server-side scoped privacy.

### Scope
- Cryptographic token generation (`billToken`, `participantToken`).
- Endpoint: `GET /api/split/{billToken}/{participantToken}`.
- Scoped query handler returning `ParticipantShareDto`.

### Dependencies
- Phase 4.

### Expected Files
- `Owezy.Application/Sharing/...`
- `Owezy.Api/Controllers/ParticipantSplitController.cs`

### Acceptance Criteria
- Participant token link resolves participant's individual share.
- Response contains ONLY that participant's items, total, and payment details.
- Response excludes other participants' financial totals or claimed items.

### Tests Required
- Privacy isolation tests (verifying Alice cannot see Bob's data).
- Token security tests (verifying non-derivability).

### Non-Goals
- Participant account creation or login prompts.

### Completion Gate
- Security privacy isolation suite passing.

---

## Phase 6 — UPI Payments & Status Confirmation

### Objective
Generate UPI payment links (`upi://pay`), allow participants to mark split as paid, and allow Splitter to confirm payment.

### Scope
- UPI URL generator (`UpiPaymentUrlBuilder`).
- Participant action: `POST /api/split/{billToken}/{participantToken}/mark-paid`.
- Splitter action: `POST /api/bills/{billId}/participants/{participantId}/confirm-payment`.

### Dependencies
- Phase 5.

### Expected Files
- `Owezy.Application/Payments/...`
- `Owezy.Api/Controllers/PaymentsController.cs`

### Acceptance Criteria
- `upi://pay` deep link correctly includes Splitter VPA, participant amount, and reference note.
- Status transition: `Unpaid` -> `MarkedPaid` -> `Confirmed`.

### Tests Required
- UPI string formatting unit tests.
- Payment state transition integration tests.

### Non-Goals
- Payment gateway webhooks or automated bank polling.

### Completion Gate
- Status state transitions verified via tests.

---

## Phase 7 — Frontend Application (Owezy.Client)

### Objective
Build responsive, lightweight mobile-first UI for Splitter workspace and Participant split view.

### Scope
- Responsive HTML5 / CSS / Vanilla JS app (or Vite).
- Splitter flow: Login (OTP) -> Create Bill -> OCR upload / Review -> Add Participants -> Claim items -> View Summary -> Share Links -> Confirm Payments.
- Participant flow: Open Link -> View Share -> Launch UPI -> Mark Paid.

### Dependencies
- Phases 1–6.

### Expected Files
- `src/Owezy.Client/index.html`
- `src/Owezy.Client/styles/main.css`
- `src/Owezy.Client/js/app.js`

### Acceptance Criteria
- Mobile responsive layout (320px to 1200px viewports).
- Clean user experience with smooth interactions and micro-animations.
- No exposed passwords or registration forms for participants.

### Tests Required
- Frontend UI browser flow verification.

### Non-Goals
- Native iOS/Android app code.

### Completion Gate
- End-to-end user workflow operational in browser.

---

## Phase 8 — Security, Hardening & Rate Limiting

### Objective
Harden API security, enforce global rate limits, input sanitization, and architecture rules.

### Scope
- Global ASP.NET Core rate limiting middleware.
- Security header injection (CSP, HSTS, X-Frame-Options).
- Input validation filters.
- NetArchTest suite execution.

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
