# CoinStack: FinanceManager Solution

## Overview
CoinStack is a Blazor (.NET 10) web application designed to help users save money and build better financial habits by combining practical budgeting tools with Cognitive Behavioral Therapy (CBT) principles. The app provides a gamified, interactive experience for tracking spending, managing budgets, and reflecting on financial decisions.

## Goals
- Empower users to save and budget effectively.
- Incorporate CBT techniques to address emotional spending and build lasting habits.
- Provide actionable feedback, streaks, and rewards to reinforce positive behaviors.

## Features
- **Dashboard:** Visualizes daily spending and savings trends with charts.
- **Buckets/Budgets:** Manage spending envelopes and monthly budgets.
- **Transactions:** Log expenses, see game results (points, messages, reflection triggers).
- **Subscriptions:** Track recurring expenses and prune unnecessary subscriptions.
- **Debts:** Manage loans, credit cards, and liabilities; project payoff dates.
- **Settings:** Configure preferences (currency, monthly start day, etc).
- **Streaks & Scoring:** Daily check-ins, streak milestones, and gamified scoring for actions.
- **CBT Reflections:** Modal prompts triggered by spending events (e.g., over-budget, impulse buys, savings dips) to increase awareness and encourage cognitive restructuring.

## How It Works
1. **Navigation:** Sidebar menu links to Dashboard, Buckets, Transactions, Subscriptions, Debts, and Settings.
2. **State Management:** MainLayout loads user state (streaks, score, pending reflections) and updates header widgets.
3. **Reflection Workflow:** When triggered, users complete CBT-style prompts; points are awarded for completion.
4. **Gamification:** Points and streaks are tracked for budgeting actions, check-ins, and reflection completions.
5. **Responsive UI:** Tailadmin/Tailwind-based layout, mobile overlay, sticky header, and dark mode toggle.

## Key Services
- **GameLoopService:** Orchestrates bucket logic, scoring, streaks, and reflection triggers.
- **ReflectionService:** Provides CBT prompts based on spending triggers.
- **ScoringService:** Calculates points for budgeting actions, streaks, and reflections.
- **BucketService, BudgetService, SubscriptionService, TransactionService:** CRUD operations for core financial entities.

## Psychological/CBT Integration
- Reflection prompts encourage users to examine emotional triggers, spending patterns, and alternative actions.
- Streaks and rewards reinforce consistency and habit formation.
- Gamified feedback motivates users to stay under budget and resist impulses.

## Future Ideas
- Guided CBT lessons and habit-building tasks.
- Personalized nudges and reflective journaling tied to spending events.
- Analytics and insights for spending patterns.
- Social/accountability features (optional).
- Expanded gamification (badges, milestones).

## Tech Stack
- .NET 10, Blazor Server
- Tailwind/Tailadmin UI
- Entity Framework Core (FinanceManager.Data)
- ApexCharts for data visualization

## Getting Started
1. Clone the repository.
2. Run `dotnet build` and `dotnet run` from the solution root.
3. Access the app at `https://localhost:5001` (or your configured port).

## License
See LICENSE file (if present).

---
This project aims to blend financial literacy with behavioral science for a more effective, engaging budgeting experience.
