# Reconnect UX Manual Verification Checklist

Last updated: 2026-02-27

Use this checklist to verify reconnect behavior in browser scenarios before closing the reconnect UX issue.

## Setup

- Run the app in Development mode.
- Open browser DevTools Network tab.
- Ensure the current page has active Blazor interactivity.

## Scenario 1 — Reconnecting state appears

1. Load any interactive page.
2. Toggle browser offline (DevTools Network: Offline).
3. Trigger any UI action that requires server interaction.

Expected:
- Reconnect modal appears.
- "Connection Lost" state is visible.
- Spinner is visible.

## Scenario 2 — Failed state + retry button

1. Keep browser offline for several seconds.
2. Wait for reconnect attempt to fail.

Expected:
- Modal transitions to "Reconnect Failed".
- Retry button is visible and clickable.

## Scenario 3 — Retry succeeds after network returns

1. While modal is in failed state, set browser back to online.
2. Click "Retry Connection".

Expected:
- Modal returns to reconnecting state briefly.
- On successful reconnect, modal closes.
- Page remains usable without manual refresh.

## Scenario 4 — Auto-retry on tab visibility change

1. Enter failed state while offline.
2. Restore network online.
3. Switch to another tab/app, then return to the app tab.

Expected:
- Visibility change triggers retry.
- Modal closes if reconnect succeeds.

## Scenario 5 — Rejected/session-expired state

1. Force a server-side condition that rejects reconnect (for example, stop app and restart with incompatible circuit state, or invalidate session state).
2. Return to app and let reconnect fail to rejected.

Expected:
- "Session Expired" state is shown.
- User can click "Refresh Page" to recover.
- No automatic hard refresh occurs before user sees the message.

## Quick pass/fail summary

- [ ] Reconnecting UI shown
- [ ] Failed UI shown
- [ ] Retry button works
- [ ] Visibility-change retry works
- [ ] Rejected state message shown
- [ ] Refresh button recovers app

If any item fails, capture the exact scenario and browser console logs before patching.
