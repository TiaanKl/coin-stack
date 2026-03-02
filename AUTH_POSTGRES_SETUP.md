# Auth + SQLite Setup Guide

Last updated: 2026-02-27

## What was implemented

- ASP.NET Core Identity foundation using `ApplicationUser`.
- Identity API endpoints are mapped under `/auth/*`.
- EF Core context now supports Identity tables.
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

## Current behavior (SQLite)

The app is configured for SQLite right now.

- Connection string key: `ConnectionStrings:FinanceManager`
- Default development DB file: `financemanager.dev.db`

## Apply migrations (SQLite)

Run this once in your environment:

```bash
dotnet ef database update --project CoinStack.Data/CoinStack.Data.csproj --startup-project CoinStack/CoinStack.csproj
```

Then run the app normally.

## Notes on user partitioning

Identity foundation is implemented.
Per-user data partitioning for domain entities (transactions, budgets, goals, etc.) is not implemented yet and remains the next auth phase.
