# CoinStack: what to improve, and what to add

This is based on the code review. The 12 bugs already fixed and pushed are left out.

Effort: **S** = a few hours, **M** = a day or two, **L** = several days or more.

## Progress

**Phase 1 is done (2026-10-29):** every item under "Calculations that are still wrong" and "Features that only half work" below. Covered by 53 new tests (257 in total). The database change is in the `Phase1Fixes` migration.

Decisions made along the way:

- **Goal money comes from savings.** "Add" moves money out of free savings into the goal (held as `SavingsState.Reserved`, still earning interest). Withdrawing, abandoning or deleting a goal moves it back. Income logged into a goal-linked savings bucket still adds to the goal directly. Once accounts exist (Part 2, item 1), this becomes a real transfer.
- **"Didn't buy" keeps the waitlist item** (hidden, with `ResistedAtUtc`), so the page can show a "saved by waiting" total.
- **Deleting a subscription or bucket is remembered** (`DismissedRecurringMerchants`), so the bank scan and later imports don't create it again. Existing subscriptions and buckets are linked to, never overwritten.
- **Variable-rate debts** show the payment at −1, +1 and +2 percentage points. **APR** compounds daily. The rate is always a percentage, so 1 means 1%.
- **The weekly recap's "streak"** is now the number of days you checked in that week.
- **New subscription cycles:** every 2 weeks, and every 6 months.

Not done in Phase 1: the mobile app keeps its own copies of the services (`MobileFinanceService`). Only its savings maths and money formatting were updated; its goal, waitlist and import screens were not changed.

## Part 1: Improve what already exists

### Calculations that are still wrong (highest priority)

- **Debt calculator (M).**
  - "Annual" compounding applies the full yearly rate every month.
  - "Simple" interest uses the amortising formula. The page's own example says R300 interest but the calculation shows R161.62.
  - "Compound" divides a lump sum over the term.
  - Total owed is overstated.
  - An entered rate of "1" is read as 100%.
  - Very large inputs can crash the page.
  - Fixed, Variable and APR all run the same calculation.
- **Savings totals (S).** Withdrawals only reduce Available, but interest and projections use Total. Both are overstated after any withdrawal.
- **Bank-import projection (M).**
  - R8k rent paid on the 3rd projects as R82,667 a month.
  - Partial statement months pull the expense estimate down.
  - Occasional income is added to every month.
  - Refunds are counted as freelance income.
- **Recurring-payment detection (M).**
  - Cost is taken per charge, not per month.
  - 14-day and 180-day gaps are classed as "monthly".
  - It overwrites subscription costs you've edited and recreates ones you deleted.
- **Waitlist readiness score (S).** It ignores the item's price, so R50 and R50,000 items score the same.
- **Money display (S).**
  - Negative amounts show as `R-1 234.56`.
  - Separators are mixed.
  - The Debt page shows "$".
  - New settings default to USD.

### Features that only half work

- **Achievements never unlock (S–M).** Nothing ever checks for them, and their XP is never awarded.
- **Weekly recap and challenges (S).**
  - Only one weekly recap can ever be generated, and its top category and streak figures are wrong.
  - Weekly challenges disappear after Monday.
- **Goals (M).**
  - A contribution just bumps a number; no money moves.
  - Overfunding is silently dropped, and a double-click adds the amount twice.
  - There's no way to edit, delete, withdraw or abandon a goal.
- **Savings buckets (S).** Nothing in the UI can mark a bucket as savings or link it to a goal, so goal funding can't happen.
- **Debt page (S–M).**
  - There's no delete or "record payment" button.
  - The calculator isn't connected to your saved debts.
  - Simulator dates ignore your debit-order day.
- **Waitlist (S).**
  - Buying an item after its cool-off still counts as an impulse buy.
  - "Didn't buy" deletes the item, so there's no "saved by waiting" total.
  - Unlocked items need a page reload before you can act on them.
- **Import revert and multi-file imports (M).**
  - Revert has no confirmation and leaves the subscriptions and buckets it created.
  - Merging several files drops the recurring proposals.
  - Writes aren't wrapped in one database transaction.

### Everyday usability gaps

- **Transactions (S).** There's no date field, so you can't log yesterday's coffee or change a date. Amounts aren't validated, and delete has no confirmation.
- **Budgets vs Home (S).** The two pages use different bank-balance figures, so they show different numbers.
- **Payday (S).** It's limited to the 1st–28th; the 29th–31st silently becomes the 1st.
- **Subscriptions (S).**
  - Category is free text rather than a link to a category.
  - Cost and debit day aren't validated.
  - Logos send every subscription name to clearbit.com.
- **Merchant matching false positives (M).** "City Of Cape Town" is classed as OnlyFans, "MR DIY" as Mr D, and "Auto Spares" as Spar.
- **Reports and Income disagree (S).** Income uses calendar months while Reports uses your payday period. Fallback History shows times in UTC.

### Gamification integrity (M)

- The under-budget streak counts days that aren't consecutive.
- With streaks off, every dashboard load re-awards the check-in.
- Points are given per expense, so splitting one purchase into 20 small ones earns 20 times the points.
- Points from CBT entries and reflections can be farmed.
- Challenges are just ticked off, with nothing checking them.
- Level-up is never celebrated.

### Foundations that make everything else easier

- **Time zones (M).** Everything runs in UTC, so entries between 00:00 and 02:00 SAST land on the previous day. Add a user time-zone setting and use `TimeProvider`.
- **A daily background job (M).** Today, auto-deductions, check-ins and reconciliation run on page load, and prerendering runs them twice. Move them into one hosted service.
- **One forecast engine (M).** Home and Bank Import each calculate their own forecast, and they will disagree.
- **One budget-period helper (S).** The same calculation is currently copied into five or more places.
- **Decide on accounts (S to decide).** Sign-in endpoints exist but no data is scoped per user. Either remove them (single-user) or add per-user data (L).
- **Small fixes (S).** The layout loads every transaction on each refresh, and the reconnect modal can't recover after a server restart.
- **Tests (M).** Add coverage for the debt calculator, PDF parser, streaks, challenges, recap and achievements.

## Part 2: New functionality, ordered by value

1. **Accounts and transfers (L).** Cheque, savings and credit card accounts with real transfers. Savings and the emergency fund become real money, and net worth becomes possible. Adding this would resolve more of the issues above than anything else.
2. **Multi-debt payoff planner (M–L).** Avalanche or snowball ordering, rolling freed-up payments into the next debt, extra and lump-sum payments, and one debt-free date. Imported FNB card and WesBank payments would reduce the right debt automatically.
3. **Budget history and envelope rollover (M).** Per-period allocations, carrying unspent money forward, and alerts at 80% and 100% of a bucket.
4. **Smarter import (M–L).**
   - Your own merchant rules.
   - A review screen that lists skipped rows with reasons.
   - A check that opening balance + transactions = closing balance.
   - Matching imported rows against ones you logged manually.
   - Guards against importing the same file twice or mixing two accounts.
5. **Subscription renewals (M).** Next billing date, a monthly total, and alerts for price changes or missed debit orders.
6. **Goal planning (S–M).** Required monthly contribution, an on-track or overdue status, and contribution history.
7. **Reports upgrade (M).** A past-period picker, category trends, year-over-year comparison and CSV export.
8. **Reminders (M).** Upcoming debit orders, a waitlist cool-off finishing, and budget thresholds.
9. **Net worth over time (M, needs item 1).** Assets minus debts, charted month by month.
10. **South African touches (S each).**
    - A TFSA contribution tracker against the annual and lifetime caps.
    - A tax-year view (March to February).
    - 13th cheque and bonus handling.
    - A "last working day" payday.
    - Debts linked to the prime rate.
11. **Export and restore (S–M).** Reset now makes a backup, but there's no manual export or restore yet.
12. **Gamification v2 (M).** One reversible reward ledger, auto-checked challenges (a no-spend day verified against your transactions), a streak freeze, and celebrations.
13. **Sync mode (L).** The shared API and server database already on your README roadmap.

## Suggested order

1. **Phase 1:** the remaining calculation bugs and the half-working features. These are small and make the existing numbers trustworthy.
2. **Phase 2:** the foundations (time zones, background job, one forecast, one period helper).
3. **Phase 3:** new features 1–5, in that order.
