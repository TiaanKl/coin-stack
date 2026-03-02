# CoinStack Mobile Runtime Validation Checklist

Last updated: 2026-03-01
Scope: MAUI Blazor Hybrid mobile MVP validation on emulator/device.

## Prerequisites

- Build succeeds: `dotnet build CoinStack.slnx`
- Android emulator or physical device available.
- App launches and reaches the Dashboard route.

## 1) Startup and DB Bootstrap

1. Launch app first time.
2. Wait for initial DB bootstrap to complete.
3. Navigate to Dashboard.

Expected:
- App opens without crash.
- Dashboard loads period totals.
- No blocking initialization error message.

## 2) Transactions Flow

1. Open Transactions page.
2. Create one expense transaction with bucket selected.
3. Create one income transaction.

Expected:
- Both records save successfully.
- New rows appear in recent list.
- Amounts use selected currency symbol/format.

## 3) Dashboard Summary

1. Return to Dashboard.
2. Press Refresh.

Expected:
- Period Income/Expense/Net reflect recent transactions.
- Recent activity list shows latest entries.

## 4) Buckets Utilization

1. Open Buckets page.
2. Check spent/remaining values.

Expected:
- Allocated/Spent/Remaining values render.
- Percent Used updates after transaction changes.

## 5) Savings

1. Open Savings page.
2. Toggle fallback on/off.

Expected:
- Toggle persists and status updates.
- Savings state renders without exceptions.

## 6) Reflections

1. Open Reflections page.
2. Create a manual reflection.
3. Complete a pending reflection.

Expected:
- Created reflection appears in pending list.
- Completed reflection moves to history with mood/emotion data.

## 7) Navigation and Stability

1. Navigate repeatedly through all pages.
2. Background app and resume.

Expected:
- No crashes or blank routes.
- Data remains available after resume.

## 8) Quick Pass/Fail Grid

- [ ] Startup/bootstrap pass
- [ ] Transactions create/list pass
- [ ] Dashboard totals pass
- [ ] Buckets utilization pass
- [ ] Savings toggle/state pass
- [ ] Reflections create/complete pass
- [ ] Navigation/resume stability pass

## If something fails

Capture:
- Page/flow where failure occurred
- Steps to reproduce
- Error message shown in UI
- Device/emulator + OS version
