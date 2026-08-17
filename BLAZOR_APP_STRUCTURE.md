# CoinStack Blazor App - Complete Page & Component Structure

## Overview
This document provides a comprehensive guide to all pages in the CoinStack Blazor application, including their sections and components used.

**Total Pages: 20**

---

## 1. **HOME (Dashboard)** - `/`
**Route:** `@page "/"`  
**RenderMode:** InteractiveServer  
**Purpose:** Primary dashboard showing financial overview, spending trends, and recent activity

### Sections:
1. **Budget Overview Card** (Top-left)
   - Monthly Budget Remaining display
   - Budget spent vs. limit information
   - Planned, Spent, and Available badges
   
2. **Reserves On Hand Card** (Top-right)
   - Savings total
   - Emergency Fund total
   - Total Reserved amount
   - Reserve-aware budget calculation (if enabled)

3. **Daily Spending Chart** (Middle-left)
   - Bar chart showing daily expenses for the past 30 days
   - Placeholder display support

4. **Money Trend Chart** (Middle-right)
   - Line chart showing cumulative money trend over 30 days
   - Placeholder display support

5. **Predictive Cash Flow Section** (Large card, bottom)
   - 30-day projection with summary stats
   - Triple-line chart: Projected Net Balance, Projected Income, Projected Expense
   - Forecast data visualization

6. **Recent Transactions Panel** (Bottom-left)
   - Lists 5 most recent transactions
   - Shows transaction description, date, and amount
   - Color-coded by transaction type (income/expense)

7. **Recent Score Activity Panel** (Bottom-right)
   - Displays recent game loop events
   - Shows points gained/lost with descriptions
   - Color-coded positive/negative events

### Components:
- `BudgetDonut` - Reusable donut chart component
- `ApexChart` (TItem: DailyData, ForecastData) - Chart visualization
- Custom record types: `DailyData`, `ForecastData`

### Key Injections:
- `ITransactionService`, `IGameLoopService`, `ISettingsService`, `ISavingsService`, `ISubscriptionService`, `IBucketService`, `IMoneyFormatter`

---

## 2. **TRANSACTIONS** - `/transactions`
**Route:** `@page "/transactions"`  
**RenderMode:** InteractiveServer  
**Purpose:** View, create, and manage all transactions (expenses and income)

### Sections:
1. **Header**
   - Title and description
   - "Log Expense" button

2. **Game Result Toast** (Conditional)
   - Shows points gained/lost from latest transaction
   - Displays feedback message
   - Links to reflection page if triggered

3. **Recent Transactions Table**
   - Columns: Date, Description, Bucket, Debt, Type, Amount, Actions
   - Inline editing and deletion
   - Category badges with colors
   - Expense kind indicators (Mandatory, Force Majeure)
   - Auto-deduct status
   - Impulse purchase indicator

4. **Transaction Modal** (Create/Edit)
   - Description input
   - Amount input
   - Bucket selector
   - Category selector (auto-populated from bucket if selected)
   - Debt account selector
   - Transaction Type selector (Expense/Income/Transfer)
   - Expense Kind selector (Discretionary/Mandatory/Force Majeure)
   - Auto-deduct toggle
   - Draw from savings toggle (for Force Majeure)
   - Impulse purchase checkbox
   - Notes textarea
   - Form error display

### Components:
- Modal backdrop with blur effect
- Inline form validation

### Key Injections:
- `ITransactionService`, `IBucketService`, `ICategoryService`, `IDebtService`, `ISettingsService`, `ISavingsService`, `IGameFeedbackService`

---

## 3. **BUDGETS (Spending Buckets)** - `/budgets`
**Route:** `@page "/budgets"`  
**RenderMode:** InteractiveServer  
**Purpose:** Manage spending buckets for budget allocation and tracking

### Sections:
1. **Header**
   - Title and description
   - Add bucket button

2. **Monthly Budget Summary** (Conditional)
   - Monthly Budget Remaining display
   - Budget spent vs. limit
   - Planned, Actually Spent, Available badges
   - Budget donut chart
   - Only shows if buckets exist

3. **Empty State** (Conditional)
   - Message when no buckets exist
   - CTA to create first bucket

4. **Bucket Grid** (Multiple cards)
   - Name and category indicator
   - Edit/Delete buttons
   - Current spending display
   - Planned vs. Actual stacked progress bars
   - Remaining balance badge
   - Percentage used indicator

5. **Create/Edit Bucket Modal**
   - Bucket name input
   - Monthly Allocation input
   - Category selector
   - Form error display
   - Save/Cancel buttons

### Components:
- `BudgetDonut` - Donut chart component
- Card-based grid layout for buckets
- Modal dialog with validation

### Key Injections:
- `IBucketService`, `ICategoryService`, `ITransactionService`, `ISettingsService`

---

## 4. **GOALS** - `/goals`
**Route:** `@page "/goals"`  
**RenderMode:** InteractiveServer  
**Purpose:** Create and track savings goals with milestones and rewards

### Sections:
1. **Goal Creation Panel** (Left sidebar)
   - Goal name input
   - Target amount input
   - Current amount input
   - Target date picker (optional)
   - Form error display
   - Create goal button

2. **Goals Grid** (Right panel)
   - Empty state message
   - Goal cards with:
     - Name and target date
     - Status badge (Active/Completed)
     - Current amount vs. target display
     - Progress bar
     - Milestone badges (25%, 50%, 75%, 100%)
     - Contribution input field
     - Add button

### Components:
- Goal cards with progress visualization
- Milestone display with unlock status
- Input field for adding contributions

### Key Injections:
- `IGoalService`, `ISettingsService`

---

## 5. **DEBT** - `/debt`
**Route:** `@page "/debt"`  
**RenderMode:** InteractiveServer  
**Purpose:** Calculate and manage personal debt with multiple interest types

### Sections:
1. **Input Parameters Panel** (Left)
   - Interest Type selector (Fixed, Variable, APR, Simple, Compound, None)
   - Information box with selected type description and examples
   - Compounding frequency selector (if applicable)
   - Principal input (optional)
   - Total Owed input (optional)
   - Interest Rate input (with dynamic label)
   - Monthly Payment input (optional)
   - Term Months input (optional)
   - Payments Made input (optional)
   - Start Date picker (optional)
   - End Date picker (optional)
   - Form error display
   - Calculate and Reset buttons

2. **Calculation Output Panel** (Right)
   - Empty state message
   - Resolved Debt Type display
   - Formula Used display
   - Results grid:
     - Principal, Total Owed, Interest Amount
     - Monthly Payment, Term, Remaining Balance
     - Start Date, End Date, Rate (normalized)
   - Inferences section (if applicable)
   - Warnings section (if applicable)

3. **Saved Debt Cards Section**
   - Add Debt button
   - Loading state
   - Empty state message
   - Debt cards grid with:
     - Name, Provider, Interest Rate (APR)
     - Current Balance vs. Total Amount
     - Progress bar
     - Monthly payment and start date
     - Edit button

4. **Debt Type Examples Section**
   - Card-based layout
   - Example calculations for: Fixed-rate, Variable-rate, APR, Simple, Compound, Zero-Interest
   - Type badge, input summary, and results display

5. **Create/Edit Debt Modal**
   - Name input
   - Provider input (optional)
   - Total Amount input
   - Current Balance input
   - Monthly Payment input
   - Interest Rate (%) input
   - Start Date picker
   - Planned Term Months input (optional)
   - Form error display
   - Save/Cancel buttons

### Components:
- Input filtering and normalized calculations
- Result display grid
- Debt card components with progress bars
- Example calculation cards

### Key Injections:
- `IDebtCalculatorEngine`, `IDebtService`

---

## 6. **CATEGORIES** - `/categories`
**Route:** `@page "/categories"`  
**RenderMode:** InteractiveServer  
**Purpose:** Manage transaction categories with color coding and scope

### Sections:
1. **Header**
   - Title and description
   - Add category button

2. **Categories Table**
   - Columns: Name (with color dot), Scope, Color (hex), Actions
   - Edit and Delete buttons for each row
   - Hover effects

3. **Empty State** (Conditional)
   - Message when no categories exist
   - CTA to create first category

4. **Create/Edit Category Modal**
   - Category Name input
   - Color picker
   - Scope selector (dropdown with Expense/Income/Both/Transfer options)
   - Form error display
   - Save/Cancel buttons

### Components:
- Simple table layout
- Color picker input
- Modal dialog with form validation

### Key Injections:
- `ICategoryService`

---

## 7. **SETTINGS** - `/settings`
**Route:** `@page "/settings"`  
**RenderMode:** InteractiveServer  
**Purpose:** Configure application preferences and settings

### Sections:
1. **Budget Preferences**
   - Currency selector (USD, EUR, GBP, CAD, AUD, ZAR)
   - Monthly Start Day selector (1-28)
   - Monthly Net Income input (with currency symbol prefix)
   - Toggle: Reserve-aware budget on home card
   - Toggle: Allow emergency fund fallback

2. **Savings Plan** (Section header visible)
   - (Settings page is truncated in read, but likely contains savings configuration)

3. **Additional sections** (Not fully visible in read)
   - Data reset options
   - Game scoring rules
   - Other application settings

### Components:
- Select/Dropdown inputs
- Number inputs
- Toggle switches
- Card-based sections

### Key Injections:
- `IScoringService`, `IDataResetService`, `ISettingsService`, `IMoneyFormatter`

---

## 8. **INCOME** - `/income`
**Route:** `@page "/income"`  
**Purpose:** View income streams and year-to-date income analysis

### Sections:
1. **Income Summary Cards** (Top)
   - Received This Month card with amount and percentage change vs. last month
   - Year To Date card with amount and average per month

2. **Income Sources Chart**
   - Donut chart showing income breakdown by category
   - Empty state message if no income data

3. **Recent Deposits List**
   - Displays last 5 income transactions
   - Shows description, date, and amount
   - Green income indicator

### Components:
- `ApexChart` (TItem: IncomeData) - Donut chart
- Summary stat cards
- Transaction list with icons

### Key Injections:
- `ITransactionService`, `ISettingsService`, `IMoneyFormatter`

---

## 9. **SAVINGS** - `/savings`
**Route:** `@page "/savings"`  
**RenderMode:** InteractiveServer  
**Purpose:** Manage savings, emergency funds, and view savings projections

### Sections:
1. **Header**
   - Title and description
   - "Apply This Month" button (with loading state)

2. **Toast Notification** (Conditional)
   - Success message for month calculations

3. **Summary Cards Grid** (6 cards)
   - Total Saved
   - Available
   - This Month (base contribution)
   - Interest Earned
   - Emergency Total
   - Emergency Available

4. **Move Budget Into Reserves**
   - Destination selector (Savings or Emergency Fund)
   - Amount input
   - Reason input
   - Add button

5. **Savings Rule Card** (Left panel)
   - Rule type display (percentage or fixed amount)
   - Interest rate (if configured)
   - Monthly income (base)
   - Link to Settings for editing

6. **Fallback Coverage Card** (Right panel)
   - Toggle: Allow savings to cover overspending
   - Used this month display (if enabled)
   - Top fallback categories list (if used)
   - Link to fallback history page

7. **Savings Projection Chart**
   - Toggle: Show interest
   - Time period selector (6/12/24/36 months)
   - Area chart: Projected Savings
   - Empty state if no savings rule

8. **Monthly History Table**
   - Columns: Month, Base, Interest, Added, Running Total
   - Sorted chronologically
   - Empty state message

### Components:
- `ApexChart` (TItem: SavingsProjectionPoint) - Area chart
- Summary stat cards
- Toggle switches
- Table display

### Key Injections:
- `ISavingsService`, `ISettingsService`, `IMoneyFormatter`

---

## 10. **ACHIEVEMENTS** - `/achievements`
**Route:** `@page "/achievements"`  
**RenderMode:** InteractiveServer  
**Purpose:** Display unlocked badges and player level progression

### Sections:
1. **Header**
   - Title and description
   - Achievements unlock progress badge (X/Total)

2. **Level Progress Card**
   - Current level display (badge)
   - Level title
   - Total XP
   - Current XP / Required XP to next level
   - Progress bar with gradient

3. **Achievements Grid** (Below level progress)
   - (Full content not visible in truncated read)

### Components:
- Progress bar with animation
- Achievement badge display
- Level badge

### Key Injections:
- `IAchievementService`, `ILevelService`

---

## 11. **CHALLENGES (Daily Challenges)** - `/challenges`
**Route:** `@page "/challenges"`  
**RenderMode:** InteractiveServer  
**Purpose:** Display and track daily challenges for earning XP

### Sections:
1. **Header**
   - Title and description
   - Completed Today badge
   - Completed This Week badge

2. **Level Progress Bar**
   - Level badge
   - Level title
   - Current XP / Required XP to next level
   - Progress bar with gradient

3. **Challenges List/Grid** (Below header)
   - (Full content not visible in truncated read)

### Components:
- Progress bar with animation
- Achievement/challenge cards
- Statistics badges

### Key Injections:
- `IDailyChallengeService`, `ILevelService`

---

## 12. **REPORTS** - `/reports`
**Route:** `@page "/reports"`  
**RenderMode:** InteractiveServer  
**Purpose:** View monthly financial report with cashflow, savings, and debt summary

### Sections:
1. **Report Header**
   - Title and description

2. **Summary Cards Grid** (4 or more cards)
   - Income (Period)
   - Expenses (Period)
   - Net Cashflow (with color coding)
   - Savings Available (with fallback usage warning)
   - (Additional cards not visible in truncated read)

3. **Additional Report Sections** (Not visible in truncated read)
   - Charts or tables for debt, buckets, or other metrics

### Components:
- Summary stat cards
- Color-coded metrics (positive/negative)
- Loading state support

### Key Injections:
- `ITransactionService`, `ISettingsService`, `ISavingsService`, `IDebtService`, `IBucketService`, `IMoneyFormatter`

---

## 13. **SUBSCRIPTIONS** - `/subscriptions`
**Route:** `@page "/subscriptions"`  
**RenderMode:** InteractiveServer  
**Purpose:** Track recurring subscription services

### Sections:
1. **Header**
   - Title
   - "Add New" button

2. **Subscriptions Table**
   - Columns: Service, Category, Cycle, Cost, Status, Debit Day, Actions
   - Edit and Delete actions
   - Loading state
   - Empty state message

3. **Create/Edit Subscription Modal** (Not visible in truncated read)
   - Form fields for subscription details

### Components:
- Table layout
- Modal dialog (implied)

### Key Injections:
- `ISubscriptionService`, `ICategoryService`

---

## 14. **CBT JOURNAL** - `/cbtjournal`
**Route:** `@page "/cbtjournal"`  
**Purpose:** Cognitive Behavioral Therapy (CBT) journal for financial mindfulness
*(Detailed structure not available in project scan)*

---

## 15. **WEEKLY RECAP** - `/weekly-recap` (WeeklyRecapPage.razor)
**Route:** (Inferred from filename)
**Purpose:** Display weekly financial summary and achievements
*(Detailed structure not available in project scan)*

---

## 16. **DEBT SIMULATOR** - `/debt-simulator` (DebtSimulator.razor)
**Route:** (Inferred from filename)
**Purpose:** Simulate debt payoff scenarios
*(Detailed structure not available in project scan)*

---

## 17. **FALLBACK HISTORY** - `/savings/fallback-history` (FallbackHistory.razor)
**Route:** (Inferred from filename)
**Purpose:** Show detailed history of fallback fund usage
*(Detailed structure not available in project scan)*

---

## 18. **ERROR PAGE** - `/error` (Error.razor)
**Route:** `@page "/error"`
**Purpose:** Display application errors

---

## 19. **NOT FOUND PAGE** - NotFound.razor
**Route:** (Default 404 handler)
**Purpose:** Display 404 not found page

---

## 20. **WAITLIST** - `/waitlist` (Waitlist.razor)
**Route:** `@page "/waitlist"`
**Purpose:** Manage waitlist functionality
*(Detailed structure not available in project scan)*

---

## Global Components

### Reusable Components:
1. **BudgetDonut** - Displays budget remaining as a donut chart
2. **ApexChart** - Generic chart wrapper supporting multiple chart types
   - SeriesType: Bar, Line, Area, Donut, etc.
   - Supports multiple data series
   - Configurable height and options

### Shared Features Across All Pages:
- Dark mode support (dark: prefixed classes)
- Responsive grid layouts (grid-cols-1, sm:grid-cols-2, lg:grid-cols-4, etc.)
- Consistent card styling (rounded-2xl border border-gray-200)
- Modal dialogs with backdrop blur
- Form validation with error displays
- Loading states
- Empty state messages
- Toast notifications for feedback

---

## Layout Components
**_Imports.razor** - Global component imports
**App.razor** - Root component
**Routes.razor** - Route configuration
**Layout/** - Layout components (navigation, headers, footers)
**Shared/** - Shared UI components

---

## Service Dependencies

### Core Services Used Across Pages:
- **ITransactionService** - Transaction CRUD operations
- **ISettingsService** - Application settings management
- **IBucketService** - Budget bucket management
- **ICategoryService** - Category management
- **ISavingsService** - Savings calculations and tracking
- **IGameLoopService** - Game state and scoring
- **IDebtService** - Debt account management
- **IMoneyFormatter** - Currency formatting utilities
- **IDebtCalculatorEngine** - Debt calculation engine
- **IGoalService** - Goal management
- **IAchievementService** - Achievement tracking
- **ILevelService** - Level/XP progression
- **IDailyChallengeService** - Daily challenge management
- **ISubscriptionService** - Subscription tracking
- **IGameFeedbackService** - Game feedback/notifications

---

## Styling
- **Framework:** Tailwind CSS
- **Theme Colors:** Dark mode support with gray, brand, success, error, warning color palettes
- **Components:** Custom form elements (toggles, selects, inputs)
- **Responsive:** Full mobile-to-desktop responsive design
- **Animations:** Smooth transitions and progress bar animations

---

## Interactive Features
- Real-time calculations (debt, savings projections)
- Chart visualizations (ApexCharts integration)
- Modal dialogs for CRUD operations
- Inline editing and deletion
- Form validation with error feedback
- Toast notifications
- Loading states
- Fallback content for empty states

---

## Data Flow
1. Pages inject required services
2. `OnInitializedAsync()` loads initial data
3. Components bind to data with `@bind` or event handlers
4. User interactions trigger service methods
5. Results update component state via `_list.Clear()` and `AddRange()`
6. Component re-renders automatically

---

**Document Generated:** May 14, 2026  
**Application:** CoinStack - Personal Finance Management System  
**Framework:** Blazor (Server-side Rendering)

