# Owezy — Agent Rules

## Context Discipline

Before beginning any task, read:
1. `.ai/AGENT_RULES.md`
2. `.ai/PROJECT_STATUS.md`
3. `.ai/CURRENT_TASK.md`
4. `.ai/HANDOFF.md` — only when directly relevant

Read only source files directly relevant to the current milestone.
Do NOT scan the entire repository unless the task genuinely requires it or a contradiction is discovered.
Do NOT reread completed milestone documentation.
Use Git history when historical information is required.
Do not duplicate information unnecessarily across AI context files.

Completed work is represented by Git history and the compact project status/handoff.
Future agents should not reread completed work unless the current task depends on a specific historical decision.

## Scope Discipline

- Implement only the explicitly requested milestone.
- Do not add speculative or "helpful" features outside the milestone.
- Do not redesign existing architecture unless the milestone requires it.
- If a requirement is ambiguous, inspect existing decisions first.
- If there is a genuine contradiction, stop and report it.

## Token Discipline

- Prefer targeted file inspection over broad scans.
- Prefer concise summaries.
- Do not generate large reports unless requested.
- At milestone completion, provide a compact milestone receipt.
- Verification reports should normally be compact. Provide detailed evidence only when a failure, ambiguity, security concern, or architectural question requires investigation.

## Architectural Rules

- Modular monolith: `Domain` ← `Application` ← `Infrastructure` ← `API`.
- `Domain` has zero external dependencies.
- `Application` depends only on `Domain`.
- `Infrastructure` depends on `Application` and `Domain`; never on `API`.
- Database: SQL Server + EF Core only. No PostgreSQL, Redis, or NoSQL.
- Monetary values: always `decimal`. `float`/`double` are prohibited.
- Rounding: Largest Remainder Method with deterministic tie-breaking.

## Forbidden v1 Features

Do not implement unless explicitly instructed: JWT, authentication API endpoints, SMS production, refresh tokens, authorization middleware, background cleanup, Redis, participant/bill/payment functionality, analytics, chat, social features, admin dashboards, native mobile apps, subscriptions, or paid tiers.

## Workflow

```
READ → UNDERSTAND → PLAN → IMPLEMENT → TEST → VERIFY → UPDATE STATUS → STOP
```

STOP after milestone completion. Do NOT proceed to the next milestone automatically.
