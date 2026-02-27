# Auth + PostgreSQL Setup Guide

Last updated: 2026-02-27

## What was implemented

- ASP.NET Core Identity foundation using `ApplicationUser`.
- Identity API endpoints are mapped under `/auth/*`.
- EF Core context now supports Identity tables.
- Database provider is now configurable: `Sqlite` or `PostgreSql`.
- Identity schema migration added: `AddIdentityFoundation`.

## Auth endpoint trigger flow

Auth endpoints are active as soon as the app is running.

Base route:
- `/auth`

Examples:
- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/logout`

These are provided by `MapIdentityApi<ApplicationUser>()` in startup.

## Current default behavior

By default, appsettings keeps provider as `Sqlite` so your app continues working without PostgreSQL installed.

- `Database:Provider = Sqlite`
- SQLite connection string key: `ConnectionStrings:FinanceManagerSqlite`

## Switch to PostgreSQL

1. Install PostgreSQL locally.
2. Create a database (for example `coinstack_dev`).
3. Update `CoinStack/appsettings.Development.json`:
   - `Database:Provider` -> `PostgreSql`
   - `ConnectionStrings:FinanceManagerPostgres` -> your real connection string
4. Apply migrations to PostgreSQL:

```bash
dotnet ef database update --project CoinStack.Data/CoinStack.Data.csproj --startup-project CoinStack/CoinStack.csproj
```

5. Run app normally.

## Notes on user partitioning

Identity foundation is implemented.
Per-user data partitioning for domain entities (transactions, budgets, goals, etc.) is not implemented yet and remains the next auth phase.
