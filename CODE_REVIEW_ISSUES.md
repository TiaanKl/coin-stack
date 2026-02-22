# Code Review Issue Requests

The following issue tickets are ready to be created manually in GitHub.

---

## 1) Build failure: `FinanceManagerDbContext` references wrong namespace and entity types

**Title suggestion:** `FinanceManager.Data: fix broken FinanceManagerDbContext entity namespace references`

**Problem**
- `FinanceManager.Data/FinanceManagerDbContext.cs:1` imports `FinanceManager.Data.Entities`.
- Actual entities are under `CoinStack.Data.Entities` (for example `FinanceManager.Data/Entities/Budget.cs:1`).
- Build/test currently fails with missing type errors (`Category`, `Subscription`, `Transaction`, etc).

**Evidence**
- `FinanceManager.Data/FinanceManagerDbContext.cs:1`
- `FinanceManager.Data/FinanceManagerDbContext.cs:12-21`
- Baseline command: `dotnet test CoinStack.slnx` fails with `CS0234` and `CS0246`.

**Expected fix direction**
- Align namespace/type usage in `FinanceManagerDbContext` to the canonical data model namespace, or remove/replace duplicate context if it is no longer intended to compile.

**Acceptance criteria**
- `dotnet build CoinStack.slnx` compiles without namespace/type errors from `FinanceManagerDbContext`.

---

## 2) Build failure: duplicate properties in `Budget` entity

**Title suggestion:** `CoinStack.Data Budget entity has duplicate Bucket navigation properties`

**Problem**
- `FinanceManager.Data/Entities/Budget.cs` defines `BucketId` and `Bucket` twice.
- This produces duplicate member compile errors.

**Evidence**
- `FinanceManager.Data/Entities/Budget.cs:10-11`
- `FinanceManager.Data/Entities/Budget.cs:16-17`
- Baseline command: `dotnet test CoinStack.slnx` fails with `CS0102` for `Budget`.

**Expected fix direction**
- Keep a single `BucketId` + `Bucket` property pair and remove the duplicate declarations.

**Acceptance criteria**
- `Budget` compiles cleanly with only one `BucketId`/`Bucket` pair.

---

## 3) Migration conflict: duplicate `AddBudgetBucketId` migration classes/operations

**Title suggestion:** `EF migrations: resolve duplicate AddBudgetBucketId migration and schema operations`

**Problem**
- Two migrations share the same class name `AddBudgetBucketId` and both add `Budgets.BucketId` + index + FK.
- This causes duplicate member/attribute compile errors and risks duplicate schema operations if both are applied.

**Evidence**
- `FinanceManager.Data/Migrations/20260221230120_AddBudgetBucketId.cs:8`
- `FinanceManager.Data/Migrations/20260221230524_AddBudgetBucketId.cs:8`
- Baseline command: `dotnet test CoinStack.slnx` fails with duplicate migration class/member errors (`CS0579`, `CS0111`).

**Expected fix direction**
- Keep only one migration path for `BucketId` introduction (delete/rename/re-scaffold as needed) and ensure migration snapshot/history is consistent.

**Acceptance criteria**
- Migration project compiles with unique migration types.
- Applying migrations does not attempt to add `Budgets.BucketId` twice.

---

## 4) Score inflation: daily check-in awards points three times

**Title suggestion:** `GameLoop: daily check-in currently adds triple points`

**Problem**
- In the non-first check-in path, the service logs the daily check-in score event three times.
- This inflates score history and total points (+6 instead of +2 per day).

**Evidence**
- `FinanceManager/Services/GameLoopService.cs:111-119`
- Three consecutive calls to `AddScoreEventAsync(... DailyCheckIn ...)` are executed for one check-in.

**Expected fix direction**
- Keep a single daily check-in score event in this flow and remove duplicate calls.

**Acceptance criteria**
- A single eligible daily check-in adds exactly one daily check-in score event and expected points.

---

## 5) Transaction edit/delete does not actually reverse prior score effects

**Title suggestion:** `TransactionService: score revert path is a no-op on update/delete`

**Problem**
- Transaction update/delete calls `RevertTransactionImpactAsync`.
- Current implementation adds a 0-point manual adjustment event, so old score effects remain.
- Re-editing or deleting transactions causes score drift from real behavior.

**Evidence**
- `FinanceManager/Services/TransactionService.cs:61`
- `FinanceManager/Services/TransactionService.cs:101`
- `FinanceManager/Services/GameLoopService.cs:302-311` (revert writes `AddScoreEventAsync(0, ...)`)

**Expected fix direction**
- Track score events per transaction (or recalculate and apply compensating delta) so update/delete can truly reverse previous impact before recalculating.

**Acceptance criteria**
- Editing/deleting a transaction produces net score consistent with current transaction set (no cumulative drift).

---

## 6) Savings expense can incorrectly increase goal progress

**Title suggestion:** `GameLoop: spending from savings bucket can advance goals`

**Problem**
- `ProcessTransactionAsync` only continues for expense transactions.
- In savings buckets, goal amount is then incremented by transaction amount.
- This can reward spending from savings by increasing `Goal.CurrentAmount`.

**Evidence**
- `FinanceManager/Services/GameLoopService.cs:169-172` (expense-only path)
- `FinanceManager/Services/GameLoopService.cs:216-227` (`linkedGoal.CurrentAmount += transaction.Amount`)

**Expected fix direction**
- Only increase goal progress for true savings contributions (income/transfer-in semantics), and avoid increasing goal amount when spending from savings.

**Acceptance criteria**
- Savings withdrawals do not increase goal progress.
- Goal progress changes only on intended contribution transaction types.

---

## 7) Goal linkage uses unrelated IDs (`Goal.Id == Bucket.Id`)

**Title suggestion:** `Savings goal mapping should use explicit relation, not matching primary keys`

**Problem**
- Goal lookup currently uses `db.Goals.FirstOrDefault(g => g.Id == bucket.Id)`.
- Goal and bucket IDs are independent identities, so this can update the wrong goal or no goal.

**Evidence**
- `FinanceManager/Services/GameLoopService.cs:222`

**Expected fix direction**
- Introduce explicit linkage (for example `Bucket.GoalId`) or a dedicated mapping table and query using that relationship.

**Acceptance criteria**
- Savings transaction updates only the goal explicitly linked to that bucket.

---

## 8) Month-start setting is ignored in bucket spend calculations shown in key UI state

**Title suggestion:** `Bucket spend/state uses calendar month instead of configured budget period`

**Problem**
- Budget totals respect `MonthStartDay` via `TransactionService.GetExpenseTotalForBudgetPeriodAsync`.
- Bucket spend calculations and game-state checks use calendar month (`year/month`), not configured period bounds.
- Users can see inconsistent values between budget totals and bucket/streak logic.

**Evidence**
- `FinanceManager/Components/Pages/Budgets.razor:201-205`
- `FinanceManager/Services/BucketService.cs:78-79`
- `FinanceManager/Services/GameLoopService.cs:50`
- `FinanceManager/Services/GameLoopService.cs:137`

**Expected fix direction**
- Unify bucket spend queries and game-loop state to use settings-based budget period boundaries.

**Acceptance criteria**
- Bucket spent amounts, under-budget checks, and budget totals all use the same configured budget period.

---

## 9) Large-expense reflection ignores user-configured threshold

**Title suggestion:** `Reflection trigger uses hardcoded 50% instead of settings threshold`

**Problem**
- Settings page persists `LargeExpenseThreshold`.
- Large-expense reflection trigger uses hardcoded `monthlyLimit * 0.5m`.
- User preference has no effect on trigger behavior.

**Evidence**
- `FinanceManager/Components/Pages/Settings.razor:382-403`
- `FinanceManager/Services/GameLoopService.cs:251-253`

**Expected fix direction**
- Read current settings in game-loop transaction processing and apply configured threshold percentage.

**Acceptance criteria**
- Changing `LargeExpenseThreshold` in settings changes reflection trigger sensitivity accordingly.

---

## 10) Gamification/reflection toggles are persisted but not enforced in main behavior

**Title suggestion:** `Settings toggles are not honored by scoring, streak, and reflection flows`

**Problem**
- App settings store `EnableScoring`, `EnableStreaks`, `EnableReflections`.
- Core behavior still awards points, updates streaks, and creates reflections without checking those flags.
- Disabled features continue to run, contrary to user settings.

**Evidence**
- Toggle persistence: `FinanceManager/Components/Pages/Settings.razor:378-403`
- Scoring/streak/reflection actions:
  - `FinanceManager/Services/GameLoopService.cs:111-119` (daily scoring events)
  - `FinanceManager/Services/GameLoopService.cs:121-159` (streak updates)
  - `FinanceManager/Services/GameLoopService.cs:256-260` (reflection creation)
  - `FinanceManager/Components/Layout/MainLayout.razor:162-166` (reflection completion score award)

**Expected fix direction**
- Centralize settings lookup and gate each behavior path by its corresponding toggle.

**Acceptance criteria**
- When toggles are off, related behavior paths do not execute (no new score events, no streak updates, no auto reflections).
