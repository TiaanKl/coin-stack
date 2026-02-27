# CoinStack Open Implementation Issues (Current)

Last updated: 2026-02-27 (Auth foundation + PostgreSQL provider switch scaffolding added)

## Session handoff note (for short prompts)

When starting a new chat, you can keep your prompt short and ask GPT to read this file first.

Suggested short prompt:
"Please read CODE_REVIEW_ISSUES.md first and continue from the current open issues."

This file tracks only the issues that are still open and should be prioritized in upcoming sessions.

---

## 1) Validate reconnect UX behavior in browser scenarios

**Title suggestion:** `Reconnect UX: manual scenario verification and polish`

**Problem**
- Reconnect state handling was hardened in JS, but browser-level behavior still requires manual verification.
- Automated tests do not currently cover reconnect UI transitions.

**Evidence**
- `CoinStack/Components/Layout/ReconnectModal.razor`
- `CoinStack/Components/Layout/ReconnectModal.razor.js`
- `RECONNECT_UX_CHECKLIST.md`

**Expected fix direction**
- Execute `RECONNECT_UX_CHECKLIST.md` scenarios in browser.
- Capture any final UX polish issues found during manual checks.

**Acceptance criteria**
- Reconnect modal reliably appears and recovers in all checklist scenarios.

---

## 2) P1/P2 product completeness backlog (after above)

**Title suggestion:** `Product readiness backlog: onboarding, reminders, multi-user`

**Problem**
- Core architecture is improving, but product readiness gaps remain.

**Expected fix direction**
- Implement in phases:
  - onboarding flow
  - reminders/notifications
  - user data partitioning on top of auth foundation
  - richer reports/insights (filters, comparisons)

**Acceptance criteria**
- Each epic is broken into deliverable stories and completed incrementally with tests.

**Status note**
- Identity auth foundation and API endpoints are now implemented.
- Full per-user data partitioning for domain entities is not implemented yet.
- Trigger/setup guidance for psychology features is documented in `PSYCHOLOGY_FEATURE_TRIGGERS.md`.
- Auth/PostgreSQL setup guidance is documented in `AUTH_POSTGRES_SETUP.md`.
