# CoinStack MAUI Strategy Tracker

Last updated: 2026-03-01
Owner: Shared (You + Copilot)
Status: Phase 4 in progress (core UX polish completed, runtime validation pending)

## 1) Objective

Create a CoinStack mobile experience using .NET MAUI with a practical, phased approach that protects current web progress and ships value quickly.

Primary goal:
- Deliver a usable mobile app with core finance workflows first.

Secondary goals:
- Reuse existing logic where safe.
- Keep SQLite-first for local/offline reliability.
- Add auth/sync after MVP is stable.

## 2) Recommended Approach

Chosen direction:
- .NET MAUI Blazor Hybrid first (faster reuse of existing Razor patterns and domain logic).

Why this approach:
- Lower delivery risk versus a full native rewrite.
- Faster path to Android MVP.
- Lets us incrementally modernize and native-optimize later.

## 3) Scope Boundaries

In scope now:
- New MAUI app scaffold.
- Shared logic extraction (incremental).
- Core mobile screens (Dashboard, Transactions, Buckets, Savings, Reflections).
- Local SQLite persistence and first-run database bootstrap.

Out of scope for first milestone:
- Full per-user cloud sync.
- Multi-device conflict resolution.
- Advanced notifications/reminders.
- Full native redesign of all screens.

## 4) Phased Plan

### Phase 1 — Foundation
- [x] Create MAUI solution/projects for mobile.
- [x] Define shared libraries and references.
- [x] Establish dependency injection wiring for mobile.
- [ ] Confirm app starts on Android emulator/device.

Exit criteria:
- Mobile app runs with baseline shell/navigation and compiles cleanly.

### Phase 2 — Data Layer (SQLite-first)
- [x] Configure local SQLite for mobile.
- [x] Wire first-run migration/bootstrap.
- [x] Validate read/write for core entities.
- [x] Add safe seed defaults where needed.

Exit criteria:
- Core data can be persisted and queried locally on device.

### Phase 3 — MVP Features
- [x] Dashboard summary screen.
- [x] Transaction list + create flow.
- [x] Buckets view.
- [x] Savings state view.
- [x] Reflection flow (including current CBT prompt patterns).

Exit criteria:
- Core daily user journey works end-to-end on mobile.

### Phase 4 — Stabilization
- [ ] Basic test coverage for critical mobile flows.
- [x] Error handling/logging baseline.
- [ ] Performance pass for startup and navigation.
- [x] UX polish for core screens.

Exit criteria:
- Mobile MVP is stable enough for internal usage/testing.

### Phase 5 — Auth and Sync (Post-MVP)
- [ ] Add auth integration to mobile app.
- [ ] Introduce user-scoped sync model.
- [ ] Define offline-first conflict handling rules.
- [ ] Add secure token storage.

Exit criteria:
- Authenticated users can safely sync mobile data with backend.

## 5) Current Status Snapshot

What is done:
- [x] Strategy approved.
- [x] Tracker established.
- [x] `CoinStack.Mobile` MAUI Blazor Hybrid project scaffolded.
- [x] `CoinStack.Mobile.Core` shared library scaffolded and referenced.
- [x] Mobile placeholder navigation/pages created (Dashboard, Transactions, Buckets, Savings, Reflections).
- [x] Solution build validated including Android target.
- [x] Mobile SQLite path wiring added (`FileSystem.AppDataDirectory`).
- [x] First-run migration/bootstrap service implemented.
- [x] Safe seed defaults implemented for `AppSettings` and base `Buckets`.
- [x] Mobile Home data probe implemented with sample transaction write action.
- [x] Mobile Transactions page now supports local create + recent list (SQLite-backed).
- [x] Mobile Dashboard page now shows period totals and recent activity.
- [x] Mobile Buckets page now shows period utilization (allocated/spent/remaining).
- [x] Mobile Savings page now shows state, monthly summaries, fallback history, and fallback toggle.
- [x] Mobile Reflections page now supports manual reflection creation, completion flow, and history.
- [x] Core mobile pages now use consistent card/KPI/form/table visual system for production-style UX.

What is next:
- [ ] Run app on Android emulator/device and validate startup manually.
- [ ] Confirm database bootstrap and sample transaction write in emulator/device runtime.
- [ ] Run end-to-end manual pass for all Phase 3 pages on emulator/device.
- [ ] Execute `MOBILE_RUNTIME_VALIDATION_CHECKLIST.md` on emulator/device.
- [ ] Complete startup/navigation performance pass.

Blockers / dependencies:
- Android emulator/device runtime validation still required to close Phase 1.

## 6) Risks and Mitigations

Risk: Shared code extraction becomes larger than expected.
- Mitigation: Extract only what each active phase needs.

Risk: Mobile UX feels too web-like initially.
- Mitigation: Ship hybrid MVP first, then native polish sprint.

Risk: Auth/sync complexity delays MVP.
- Mitigation: Keep auth/sync strictly post-MVP.

## 7) Working Agreement (How we use this file)

- Update this file at the end of each meaningful implementation session.
- Move checkbox state as tasks progress.
- Add short entries to the change log with date and outcome.
- Keep this file focused on MAUI strategy and progress only.

## 8) Session Change Log

- 2026-03-01: Initial MAUI strategy tracker created with approved phased plan.
- 2026-03-01: Phase 1 scaffold implemented (MAUI app + shared core + DI + placeholder mobile navigation).
- 2026-03-01: Full solution build passed including `net10.0-android` target.
- 2026-03-01: Phase 2 SQLite wiring implemented (mobile DB path, migrations/bootstrap, seed defaults, read/write probe).
- 2026-03-01: Full multi-target MAUI build passed after Phase 2 changes.
- 2026-03-01: Phase 3 started — Transactions page upgraded from placeholder to working SQLite list/create flow.
- 2026-03-01: Phase 3 continued — Dashboard and Buckets pages upgraded to live SQLite-backed views.
- 2026-03-01: Phase 3 continued — Savings and Reflections pages upgraded to live SQLite-backed views.
- 2026-03-01: Phase 4 started — added runtime validation checklist and cross-page money formatting consistency helper.
- 2026-03-01: Phase 4 continued — completed page-level visual polish pass for Dashboard, Transactions, Buckets, Savings, and Reflections; full solution build passed.

## 9) Next Action Candidate

If approved in the next prompt:
- Execute runtime checklist results and apply final stabilization fixes.
