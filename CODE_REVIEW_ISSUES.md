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
