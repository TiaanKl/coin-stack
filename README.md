# CoinStack (FinanceManager)

[![License](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-sa/4.0/)
![Status](https://img.shields.io/badge/status-pre--release-red)
![Version](https://img.shields.io/badge/version-v0.9.0--beta-orange)

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-512BD4?style=for-the-badge&logo=blazor&logoColor=white)

![Made With Love](https://img.shields.io/badge/Made%20with%20%E2%9D%A4%EF%B8%8F-by%20Tiaan%20Kloppers-blue)

CoinStack is a cross-platform personal finance app focused on practical money management and long-term behavior change.

- **Web:** Blazor Server (`CoinStack`)
- **Mobile:** native .NET MAUI (`CoinStack.Mobile`)
- **Data:** EF Core + SQLite (current runtime)

It combines budgeting features with gamified feedback (score/streaks/reflections) to help users build consistent financial habits.

## Table of Contents

- [Features](#features)
- [Platforms](#platforms)
- [Current Architecture](#current-architecture)
- [Roadmap](#roadmap)
- [Repository Structure](#repository-structure)
- [Getting Started](#getting-started)
- [Build and Run](#build-and-run)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [Known Gaps / Open Work](#known-gaps--open-work)
- [License](#license)

## Features

### Core modules
- Dashboard
- Transactions
- Buckets/Budgets
- Goals
- Savings
- Subscriptions
- Debt
- Debt Simulator
- Reports
- Waitlist
- Settings

### Behavior layer
- Daily check-ins and streak progression
- Score event tracking for budget behaviors
- Reflection prompts for key triggers (over-budget, impulse spend, savings dip)

## Platforms

- **Blazor web app**: primary full-featured experience
- **MAUI mobile app**: native mobile shell with major module parity (actively iterated)

## Current Architecture

- .NET 10 solution with shared domain entities and data layer
- EF Core + SQLite persistence
- Service-oriented business logic (transactions, scoring, reflections, savings, debt, reporting)

## Roadmap

### Product roadmap
- Deeper analytics and richer report filters
- Reminder/notification workflows
- More automation around goals/savings/debt planning
- Continued mobile UX and parity polish

### Platform/infra roadmap
- Shared backend API for Blazor + MAUI
- Server-hosted central database for sync-enabled users
- Dual data mode:
	1. Local-only (offline-first)
	2. Synced (API + server DB)

This allows users to choose between privacy/local control and cross-device sync convenience.

## Repository Structure

- `CoinStack/` – Blazor Server app
- `CoinStack.Mobile/` – native MAUI app
- `CoinStack.Mobile.Core/` – shared mobile helper abstractions
- `CoinStack.Data/` – EF Core DbContext, entities, migrations
- `CoinStack.Tests/` – unit/integration test project
- `Aspire.CoinStack/` – Aspire app host/orchestration

## Getting Started

### Prerequisites
- Git
- .NET 10 SDK
- Visual Studio 2022 (17.10+ recommended) with:
	- .NET Multi-platform App UI development workload
	- ASP.NET and web development workload
	- (Optional) Desktop development with C++ if you need Android emulator acceleration support

### Mobile platform requirements

- **Windows desktop target**
	- Windows 10 (19041+) or Windows 11
	- Windows App SDK dependencies installed by Visual Studio workloads

- **Android target**
	- Android SDK + platform tools (installed via Visual Studio)
	- At least one Android Emulator image (or a physical device with USB debugging enabled)
	- Java SDK (managed by Visual Studio/Android tooling)

### One-time setup

Restore dependencies:

```bash
dotnet restore CoinStack.slnx
```

Install MAUI workloads (only needed once per machine):

```bash
dotnet workload install maui
```

Optional, if mobile build/runtime dependencies drift:

```bash
dotnet workload restore
```

Verify installed SDKs/workloads:

```bash
dotnet --list-sdks
dotnet workload list
```

Trust local HTTPS development certificate (for web app localhost HTTPS):

```bash
dotnet dev-certs https --trust
```

### Clone

```bash
git clone https://github.com/<your-username>/coin-stack.git
cd coin-stack
```

## Build and Run

### Build whole solution

```bash
dotnet build CoinStack.slnx -c Release
```

### Run web app

```bash
dotnet run --project CoinStack/CoinStack.csproj
```

Then open the URL shown in terminal (typically `https://localhost:7xxx` or `http://localhost:5xxx`).

### Run mobile app (Windows)

```bash
dotnet build CoinStack.Mobile/CoinStack.Mobile.csproj -t:Run -f net10.0-windows10.0.19041.0
```

### Run mobile app (Android)

```bash
dotnet build CoinStack.Mobile/CoinStack.Mobile.csproj -t:Run -f net10.0-android
```

This requires an Android emulator/device running and visible to Visual Studio/ADB.

### Build mobile app (without running)

```bash
dotnet build CoinStack.Mobile/CoinStack.Mobile.csproj -c Release
```

## Testing

Run tests:

```bash
dotnet test CoinStack.Tests/CoinStack.Tests.csproj
```

## Troubleshooting

### Build fails with missing MAUI workloads

Run:

```bash
dotnet workload install maui
dotnet workload restore
```

If still failing, close Visual Studio/VS Code and rerun the commands in a fresh terminal.

### Android device/emulator not detected

- Start an emulator from Visual Studio's Android Device Manager first.
- For physical devices, enable Developer Options + USB Debugging.
- Verify ADB can see the device:

```bash
adb devices
```

### HTTPS/localhost certificate issues on web app

Run:

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

Then restart the browser and rerun the app.

### `dotnet run` starts but page does not load

- Confirm the terminal output URL and open that exact address.
- Check for port conflicts from other running apps.
- Try forcing a specific URL:

```bash
dotnet run --project CoinStack/CoinStack.csproj --urls "https://localhost:7043;http://localhost:5043"
```

### NuGet restore/package issues

```bash
dotnet nuget locals all --clear
dotnet restore CoinStack.slnx
```

### Clean rebuild when in a bad state

```bash
dotnet clean CoinStack.slnx
dotnet build CoinStack.slnx -c Release
```

## Known Gaps / Open Work

- Reconnect UX still requires final manual scenario verification
- Shared API/server sync mode is planned but not yet production-integrated
- Mobile parity is strong but still under active QA and UX refinement

## License

This project is licensed under **CC BY-NC-SA 4.0**.

You may copy, use, and modify the project with attribution, but you may not use it commercially.

See [LICENSE](LICENSE) for full details.
