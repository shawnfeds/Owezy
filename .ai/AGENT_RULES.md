# Owezy — AI Agent Operational Rules & Governance

These rules are permanent operational constraints for any AI agent working on the Owezy project. Every AI agent MUST strictly adhere to these rules.

---

## 1. Context & Token Discipline
- **Do NOT read the entire repository repeatedly.**
- Inspect ONLY the files directly relevant to the current active milestone (`.ai/PROJECT_STATUS.md`, `.ai/CURRENT_TASK.md`, relevant specs, ADRs, target source files, and test files).
- Do not reload unchanged files or perform broad repository scans for narrowly scoped tasks.

---

## 2. Strict Scope Control Firewall
- **v1 Scope is STRICTLY Controlled.**
- **DO NOT IMPLEMENT** any feature outside the agreed v1 specification.
- Specifically forbidden features:
  - Notifications (push, email, SMS alerts except basic OTP)
  - Chat or messaging
  - Social features or friend lists
  - Analytics dashboards, charts, or reports
  - Expense categorization or budgeting
  - Recurring bills or scheduling
  - Payment processing, bank gateways, escrow, or automatic transaction verification
  - Multiple currencies (INR `₹` is the standard v1 currency)
  - Subscriptions or paid tiers
  - Admin dashboards
  - Recommendation systems or AI assistants
  - Redis, Kubernetes, Event Buses, Microservices, or external cloud queues
  - Native mobile applications
- If a potentially useful out-of-scope feature is identified, record it in `.ai/PROJECT_STATUS.md` under Out-of-Scope Observations. **DO NOT IMPLEMENT IT.**

---

## 3. Implementation Loop & Workflow
Every milestone MUST follow this exact sequence:
```text
READ -> UNDERSTAND -> PLAN -> IMPLEMENT -> TEST -> VERIFY -> UPDATE STATUS -> STOP
```
- **STOP Condition**: Once a milestone is completed and verified, STOP and present the completion report. Do NOT proceed to the next milestone automatically.

---

## 4. Architectural Rules
- **Modular Monolith**: Single .NET 10 Solution (`Owezy.slnx`).
- Layer boundaries: `Owezy.Api` -> `Owezy.Application` -> `Owezy.Domain` <- `Owezy.Infrastructure`.
- Directional dependency enforcement (`Domain` has zero dependencies; `Application` depends only on `Domain`; `Infrastructure` depends on `Application` & `Domain`).
- Module organization inside `Owezy.Application`: `Auth/`, `Billing/`, `OCR/`, `Splitting/`, `Payments/`, `Sharing/`.
- **Database**: Microsoft SQL Server with Entity Framework Core. No PostgreSQL, no NoSQL, no Redis.
- **Monetary Precision**: Always use `decimal`. Floating-point types (`float`, `double`) are strictly prohibited for financial calculations.
- **Rounding Algorithm**: **Largest Remainder Method** with deterministic tie-breaking.
- **Splitter vs Participant Privacy & Security**:
  - Splitters authenticate via Phone + OTP -> JWT and have access to full bill state and history.
  - Participants access via secure relationship link `/split/{billToken}/{participantToken}` without login.
  - Participant permissions: **"Scoped read access + limited payment-status mutation"** (reading own share/items/total/splitter UPI VPA + mutating only own payment status to paid).
  - The API MUST enforce server-side authorization so participants see ONLY their own split.

---

## 5. Testing & Verification Rules
- Code edits MUST be accompanied by corresponding automated unit/integration tests.
- Never declare a milestone complete until all tests pass cleanly.
- Use `Owezy.ArchitectureTests` to strictly enforce layer boundary rules.
