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

**Direction (decided 2026-10-29): local-first, for everyone, no data collection.**

- Each install belongs to one person and keeps its data on that device. There are no accounts and no server, so the unused `/auth` sign-in endpoints are removed and no per-user data is needed.
- The desktop and mobile apps are what ships. The web app is something people run on their own machine, never a hosted site.
- A new phone of the same kind gets its data back from the phone's own backup (iCloud Backup, Android Auto Backup). An encrypted backup file covers everything else (Part 2, item 11). Live sync comes later, through the person's own cloud storage (item 13).
- Android goes back into the mobile app's target frameworks.

**Phase 2 is done (2026-10-29):** all of the foundations below except tests, which grew with each step: 37 new tests (294 in total). The database changes are in the `RemoveSignIn` and `DailyCatchUp` migrations.

- **One budget period** (`BudgetPeriod`) replaces the nine copies. Payday can be any day from 1 to 31; in a shorter month it falls on the last day. Income, Reports and the budgets now use the same period. A salary that always arrives on the month's last day is detected as payday 31.
- **Sign-in removed**, including the unused `AspNet*` tables.
- **Mobile on CoinStack.Core.** `MobileFinanceService` is now a thin adapter in CoinStack.Mobile.Core over the shared services, so mobile follows the same rules: goal money moves out of savings, waitlist purchases are recorded as expenses, and so on. Android builds again. The unused Blazor Hybrid leftovers (`Components/`, `wwwroot/`) are gone.
- **Time zones.** Days, weeks, months and pay periods follow the device's calendar (`LocalCalendar` on top of `TimeProvider`). Stored times are shown in local time.
- **Daily catch-up** (`DailyCatchUpService`) replaces the page-load jobs. It runs auto-deductions (for every pay period since the last run, up to 12), debt interest, waitlist cool-offs, challenges, weekly recaps and bank reconciliation once per local day. The web app runs it at startup, after midnight and when a page opens. Desktop runs it at startup and hourly; mobile at startup and when the app comes back to the front.
- **One forecast** (`ForecastService`). Home's 30 days are built from the same monthly income and spending that Bank Import projects. Salary lands on payday, subscriptions on their debit day, and the rest follows the weekdays that usually cost more.

Also fixed along the way, from the lists below: payday 29th–31st, Reports and Income disagreeing, Fallback History showing UTC times, and the check-in being re-awarded on every dashboard load with streaks off.

Not done in Phase 2: the mobile screens still lack the web's goal withdraw/abandon, waitlist "Didn't buy" and import screens. Android only builds; it hasn't been run on a device. iOS needs a Mac to build.

**Smarter import, part 1: learning (2026-10-29).**

- **Merchant matching** works on whole words, and the longest match wins, which fixes the false positives listed below. Personal payees are no longer built in. Common South African merchants (streaming, fuel, telcos, fast food, Uber vs Uber Eats) are recognised.
- **Learning from your choices** (`MerchantRule`, migration `MerchantRules`):
  - Moving a whole merchant to another category in the import preview is remembered for every later import.
  - So is changing a bank transaction's category on the Transactions page, which also offers to move that merchant's earlier transactions.
  - A shop the app doesn't know follows the choice made for its other branches.
- **Income per month:** a multi-month statement shows income and spending per month, and salary is the usual total per month rather than the average per payment, so fortnightly pay counts in full.
- **Styling:** the web app's CSS is now built by Tailwind from the pages (`npm run css` in `CoinStack/`), so classes no longer go missing.
- **Next:** reading any bank's statement (Part 2, item 4).

**Encrypted backup and restore (2026-10-29)**, Part 2 item 11:

- **Backup file:** one `.coinstack` file holding the whole database, compressed and encrypted with the person's password. It uses AES-256 with an HMAC check, and keys from PBKDF2-SHA256 at 600 000 rounds.
- **In each app:** on the web it downloads, desktop uses the save dialog, and mobile uses the share sheet (Files, iCloud Drive, Google Drive). Any of the apps can restore it, after showing what the backup holds.
- **Safety:** a wrong password, a damaged file or a backup from a newer app version is refused before anything changes. Restoring saves a copy of the current data first.
- **Privacy:** subscription logos no longer come from clearbit.com, which received every subscription's name; known merchants use the app's own icons.
- **Not done:** automatic scheduled backups to a chosen folder. The web app still loads its fonts and icons from Google Fonts and cdnjs; bundling them would make it fully offline.

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
- ~~**Merchant matching false positives (M).**~~ Fixed: "City Of Cape Town" was classed as OnlyFans, "MR DIY" as Mr D, and "Auto Spares" as Spar.
- **Reports and Income disagree (S).** Income uses calendar months while Reports uses your payday period. Fallback History shows times in UTC.

### Gamification integrity (M)

- The under-budget streak counts days that aren't consecutive.
- With streaks off, every dashboard load re-awards the check-in.
- Points are given per expense, so splitting one purchase into 20 small ones earns 20 times the points.
- Points from CBT entries and reflections can be farmed.
- Challenges are just ticked off, with nothing checking them.
- Level-up is never celebrated.

### Foundations that make everything else easier

- **Time zones (M).** Everything runs in UTC, so entries between 00:00 and 02:00 SAST land on the previous day. Every app runs on the person's own device, so use the device's time zone through `TimeProvider`; no setting is needed.
- **A daily catch-up (M).** Today, auto-deductions, check-ins and reconciliation run on page load, and prerendering runs them twice. A phone app can't count on running at midnight, so replace them with one catch-up that runs when the app opens (and once a day while it stays open) and handles every day missed since the last run, once.
- **One forecast engine (M).** Home and Bank Import each calculate their own forecast, and they will disagree.
- **One budget-period helper (S).** The same calculation is currently copied into nine places.
- **Accounts: decided.** Local-first with one person per install (see Progress), so the sign-in endpoints are removed.
- **Mobile on CoinStack.Core (M–L).** The mobile app keeps its own copies of the services (`MobileFinanceService`), so every fix has to be made twice. Mobile already references CoinStack.Data and Core only builds on Data, so mobile can use Core directly. Add Android back to its targets at the same time.
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
11. **Encrypted backup and restore (S–M). Done (2026-10-29).** One file, encrypted with a password the person picks, saved through the system's save dialog or share sheet. Those already offer iCloud Drive, Google Drive, OneDrive and local folders, so no cloud-provider code is needed. Covers iPhone to Android, phone to PC, and the desktop app. Reset already makes a local backup.
12. **Gamification v2 (M).** One reversible reward ledger, auto-checked challenges (a no-spend day verified against your transactions), a streak freeze, and celebrations.
13. **Sync between devices (L).** No server. Each device writes encrypted change files to a folder in the person's own cloud storage and merges the others' changes. It needs change tracking and conflict rules, so it comes after backup and restore.

## Suggested order

1. **Phase 1:** the remaining calculation bugs and the half-working features. These are small and make the existing numbers trustworthy.
2. **Phase 2:** the foundations, in this order: one period helper, remove sign-in, mobile on CoinStack.Core (so the rest is only done once), time zones, the daily catch-up, then one forecast.
3. **Phase 3:** encrypted backup and restore (item 11, done), then new features 1–5, in that order.
