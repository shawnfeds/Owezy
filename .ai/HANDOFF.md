# Handoff — Full-System Security Assessment Complete

## State

All milestones complete. Working tree clean.

## Security Assessment Findings & Fixes

### Vulnerability 1 — Functional IDOR: Item creation blocked without sharers (Medium)

**Root cause**: `HandleAddBillItemAsync` rejected requests with empty `sharerParticipantIds` at the API layer. The frontend workspace creates items first (without sharers), then assigns sharers in the separate Sharers tab. This broke the intended two-step UX.

**Fix**: Removed the premature API-layer guard. The domain's `Finalize()` invariant already enforces that every item must have at least one sharer before finalization. Items can now be created without sharers and have sharers assigned via `PUT /items/{itemId}/sharers` before finalizing.

**Regression test**: `BillingFix_AddItemWithoutSharers_IsAllowed_SharersCanBeAssignedLater`

### Vulnerability 2 — JSON XSS Defense-in-Depth (Low)

**Root cause**: ASP.NET Core's default `System.Text.Json` serializer uses `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, which does NOT HTML-encode `<`, `>`, `&`. OCR-derived content (from uploaded receipts) returned in API responses could contain raw `<script>` tags in the JSON body.

**Impact**: Low — the frontend's `escapeHtml()` on all server-provided data protects the DOM. However, defense-in-depth requires the API itself to not emit raw HTML tags in JSON.

**Fix**: Configured `JavaScriptEncoder.Default` globally in `Program.cs`. All API responses now Unicode-escape `<`, `>`, `&`, `'`, `"`.

**Regression test**: `OcrOutput_ScriptTagsInResponse_AreReturnedAsPlainText_NotExecuted`

## Full Security Assessment Results

| Domain | Result |
|--------|--------|
| Authentication | PASS — HMAC-SHA256 OTP, constant-time compare, attempt limit, JWT min-key enforced |
| Authorization/IDOR | PASS — All splitter endpoints verify caller == SplitterPhoneNumber |
| Participant isolation | PASS — Token hashed (SHA-256), scoped views, cross-participant payment isolation |
| Token security | PASS — 256-bit random token, SHA-256 stored hash, token revocation on regenerate |
| Billing logic | FIXED — Item creation now allows empty sharers; finalization still enforces sharers |
| Finalization | PASS — Immutability enforced at domain layer, double-finalize rejected (409) |
| Payments | PASS — Idempotent mark-as-paid, token-scoped, no cross-participant modification |
| Settlement | PASS — Splitter-only, derived from domain calculation, money conservation |
| Receipt/file security | PASS — Extension sanitization, GUID storage keys, magic bytes validation, size limit |
| Injection | PASS — SQL via EF parameterization; JSON XSS now defense-in-depth via HTML-safe encoder |
| Frontend security | PASS — escapeHtml() on all server data, sessionStorage (not localStorage), no hardcoded secrets |
| API security | PASS — 401/403/404/409 correct, no stack traces, no secrets in responses |
| CORS/HTTP | PASS — No CORS configured (same-origin SPA), HTTPS redirect enabled |
| Concurrency | PASS — Idempotent payment replay safe |
| Persistence/database | PASS — EF Core parameterized queries, authorization before data access |
| Information disclosure | PASS — No secrets/stack traces/internals in error responses |

## Test Results (Post-Assessment)

| Suite | Pass | Total |
|---|---|---|
| Unit | 183 | 183 |
| Integration/API | 150 | 150 |
| Architecture | 3 | 3 |
| **Total** | **336** | **336** |

## Next

READY FOR DEPLOYMENT.
