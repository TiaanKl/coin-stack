# CoinStack Mobile — Android Release: Gap Analysis & Action Plan

> Comprehensive analysis synthesizing: Blazor ↔ MAUI feature parity, mobile design guidelines, Material Design 3, .NET MAUI performance optimization, and enterprise architecture patterns.

---

## Table of Contents

1. [Feature Parity: Blazor vs MAUI](#1-feature-parity-blazor-vs-maui)
2. [Design & UX Gap Analysis](#2-design--ux-gap-analysis)
3. [Material Design 3 Gap Analysis](#3-material-design-3-gap-analysis)
4. [Performance Gap Analysis](#4-performance-gap-analysis)
5. [Architecture Gap Analysis](#5-architecture-gap-analysis)
6. [Prioritized Action Plan](#6-prioritized-action-plan)
7. [Implementation Phases](#7-implementation-phases)

---

## 1. Feature Parity: Blazor vs MAUI

### Page-by-Page Comparison

| Blazor Page | MAUI Page | Status | Gaps |
|---|---|---|---|
| Home.razor | DashboardPage.cs | ✅ Exists | Missing: animated gamification header (level/XP bar), quick-action cards with live stats |
| Transactions.razor | TransactionsPage.cs | ✅ Exists | Missing: search/filter, category icons, paginated list |
| Budgets.razor | BucketsPage.cs | ✅ Exists | Missing: donut chart visualization (Blazor has ApexCharts donut), progress animations |
| Categories.razor | CategoriesPage.cs | ✅ Exists | Missing: icon picker, color assignment |
| Income.razor | IncomePage.cs | ✅ Exists | Feature parity looks adequate |
| Goals.razor | GoalsPage.cs | ✅ Exists | Missing: progress ring visualization, milestone animations |
| Savings.razor | SavingsPage.cs | ✅ Exists | Missing: 12-month projection chart (Blazor has ApexCharts bar chart) |
| Subscriptions.razor | SubscriptionsPage.cs | ✅ Exists | Adequate |
| Debt.razor | DebtPage.cs | ✅ Exists | Missing: payoff comparison chart (avalanche vs snowball) |
| DebtSimulator.razor | DebtSimulatorPage.cs | ✅ Exists | Missing: interactive charts |
| Achievements.razor | AchievementsPage.cs | ✅ Exists | Missing: achievement unlock toast/popup, progress animations |
| Challenges.razor | ChallengesPage.cs | ✅ Exists | Missing: timer/countdown UI, confetti on completion |
| CbtJournal.razor | CbtJournalPage.cs | ✅ Exists | Adequate |
| Reports.razor | ReportsPage.cs | ✅ Exists | Missing: all charts (spending by category, income vs expense, trend lines) |
| Settings.razor | SettingsPage.cs | ✅ Exists | Missing: theme toggle (dark mode), export |
| WeeklyRecapPage.razor | WeeklyRecapMobilePage.cs | ✅ Exists | Missing: recap charts, XP summary visuals |
| FallbackHistory.razor | FallbackHistoryPage.cs | ✅ Exists | Adequate |
| Waitlist.razor | WaitlistPage.cs | ✅ Exists | Adequate |
| — | ReflectionsPage.cs | ✅ MAUI-only | Additional feature in MAUI |
| — | MoneyHubPage.cs | ✅ MAUI-only | Navigation hub (no Blazor equivalent needed) |
| — | GoalsHubPage.cs | ✅ MAUI-only | Navigation hub |
| — | GrowthHubPage.cs | ✅ MAUI-only | Navigation hub |
| — | MoreHubPage.cs | ✅ MAUI-only | Navigation hub |

### Critical Feature Gaps

| Gap | Severity | Description |
|---|---|---|
| **No Charts** | 🔴 Critical | Blazor uses ApexCharts for donut, bar, line, and area charts across 6+ pages. MAUI has only text-based progress bars. |
| **No Dark Mode** | 🔴 Critical | Blazor has full light/dark toggle via localStorage. MAUI has zero dark mode support — all colors are hardcoded light. |
| **No Game Feedback Toast** | 🟡 High | Blazor shows animated toast for XP gains/level-ups/achievements with drain bar animation. MAUI has no toast/snackbar system. |
| **No Search/Filter** | 🟡 High | Blazor's transaction/subscription lists have search and category filtering. MAUI lists are static. |
| **No Level/XP Display** | 🟡 High | Blazor shows level badge + XP progress bar in header. MAUI dashboard references level data but has no persistent header. |
| **Missing Animations** | 🟠 Medium | Blazor has CSS transitions and loading shimmer. MAUI has no animations. |
| **No Export** | 🟠 Medium | Blazor has CSV/PDF export on Reports. MAUI has none. |

---

## 2. Design & UX Gap Analysis

Based on: businessofapps.com, catdoes UX research, Medium mobile design guide.

### Current State Issues

| Guideline | Current State | Required State |
|---|---|---|
| **Touch Targets** | Inconsistent — some buttons have 48px height, but many linked labels have no padding | Minimum 44×44pt (48dp ideal) for ALL interactive elements |
| **Visual Hierarchy** | Flat — all text is similar size, no clear scanning pattern | 1 primary action per screen, clear F-pattern/Z-pattern hierarchy |
| **Loading States** | None — pages flash blank during async load | Skeleton screens or shimmer placeholders during LoadAsync() |
| **Error States** | Empty — no empty state illustrations | Friendly empty states with illustration + CTA for all lists |
| **Onboarding** | None | First-run welcome flow with key feature highlights |
| **Thumb Zone** | Bottom tabs ✅, but CTAs are often at top | Primary actions in bottom 60% of screen (thumb-reachable zone) |
| **Scroll Depth** | Long scrolling pages with all content loaded | Lazy loading sections, collapsible cards, progressive disclosure |
| **Feedback** | No haptic, no sound, no visual confirmation | Toast/snackbar on mutations, haptic on key actions |
| **Dark Mode** | Not implemented | 82% of users prefer dark mode — mandatory for release |
| **Accessibility** | Not tested | WCAG 2.2 AA: color contrast ratios, SemanticProperties on all elements, screen reader support |

---

## 3. Material Design 3 Gap Analysis

Based on: m3.material.io color, typography, elevation, layout, components, interaction states.

### Color System

**Current**: 10 hardcoded colors in `AppColors.cs` (Dark #202020, Accent #c9f158, Background #f2f3f5, Surface #ffffff, etc.) with inline `Color.FromArgb()` overrides scattered in pages.

**Required M3 Color System**:
- 26+ color roles organized in 5 groups: Primary, Secondary, Tertiary, Error, Surface
- Each role has: base, on-color, container, on-container variants
- Dynamic color support (user wallpaper extraction on Android 12+)
- 3 contrast levels: Standard, Medium, High (accessibility)
- Dark theme with all role variants
- Fixed accent colors that remain constant across themes

**Action Items**:
- [ ] Create `M3ColorScheme` with all 26+ role tokens for light theme
- [ ] Create matching dark theme token set
- [ ] Replace all hardcoded colors with semantic tokens
- [ ] Remove all inline `Color.FromArgb()` from pages
- [ ] Implement theme switching (light/dark/system)
- [ ] Consider Android 12+ dynamic color (MaterialYou)

### Typography

**Current**: Space Grotesk font at various hardcoded sizes (28pt, 20pt, 16pt, 14pt, 13pt, 12pt, 11pt). No systematic type scale.

**Required M3 Typography**:
- 5 type roles: Display, Headline, Title, Body, Label
- 3 sizes per role: Large, Medium, Small
- 15 base type styles (+ 15 emphasized variants = 30 total)
- Variable font support (Roboto Flex recommended, or keep Space Grotesk with proper scale)
- Emphasized tokens for visual hierarchy (different weight/tracking)

**Action Items**:
- [ ] Define `M3TypeScale` static class mapping all 15 base styles to Space Grotesk sizes/weights
- [ ] Replace all hardcoded font sizes with type scale tokens
- [ ] Add emphasized variants for key UI elements
- [ ] Ensure minimum 12sp for readable body text

### Elevation

**Current**: `Border` elements with `Shadow` (some cards) and `RoundRectangle` clips. No systematic elevation.

**Required M3 Elevation**:
- Levels 0–4 (not dp-based shadows)
- Tonal surface colors indicate elevation (surface → surface+1 → surface+2, etc.)
- Dark theme uses tonal elevation (lighter surface = higher elevation)
- Shadow only as supplementary visual cue

**Action Items**:
- [ ] Define 5 elevation levels as tonal surface color variants
- [ ] Apply consistently: Level 0 (background), Level 1 (cards), Level 2 (navigation), Level 3 (modals), Level 4 (overlays)
- [ ] Remove inconsistent shadow usage

### Components Mapping

| M3 Component | Current MAUI Implementation | Required Change |
|---|---|---|
| **Navigation Bar** | Shell TabBar with 5 tabs | Restyle to M3: pill-shaped active indicator, icon+label, 80dp height |
| **Cards** | Border + RoundRectangle (16pt radius) | 3 variants: Elevated (shadow + tonal), Filled (tonal surface), Outlined (stroke) — M3 uses 12dp radius |
| **Buttons** | Button style (48px, 24pt corners) | 5 variants: Filled, Tonal, Elevated, Outlined, Text. M3 uses 20dp fully-rounded corners |
| **FAB** | None | Add FAB on key pages (Transactions, Goals) for primary action |
| **Top App Bar** | None (just page title) | M3 small/medium top app bar with back nav + overflow menu |
| **Snackbar/Toast** | None | Add for all mutation confirmations + game events |
| **Chips** | None | Add for category filters, transaction tags |
| **Progress Indicators** | Basic ProgressBar | M3 linear + circular determinate/indeterminate |
| **Switches/Toggles** | Switch | Restyle to M3 with track + thumb + icon |
| **Text Fields** | Entry with basic styling | M3 filled/outlined text field with supporting text + labels |
| **Bottom Sheets** | None | Add for contextual actions, filters |
| **Dialogs** | Basic DisplayAlert | M3 full-width dialogs with proper typography |

### Interaction States

**Current**: No state changes on any elements. Buttons have no pressed/disabled styling.

**Required M3 States** (all interactive elements):
- Enabled (default)
- Disabled (38% opacity on content, 12% on container)
- Hovered (8% state layer) — less relevant for mobile
- Focused (12% state layer + focus ring)
- Pressed (12% state layer + ripple)
- Dragged (16% state layer)

**Action Items**:
- [ ] Implement state layer overlay system for all interactive elements
- [ ] Add ripple effect on tap (Android native ripple via platform-specific handler)
- [ ] Implement disabled states with proper opacity
- [ ] Add focus indicators for accessibility

### Shape

**Current**: 16pt corner radius on most cards, 24pt on buttons.

**Required M3 Shape**:
- Corner families: Rounded (default) and Cut
- Scale: None (0dp), Extra Small (4dp), Small (8dp), Medium (12dp), Large (16dp), Extra Large (28dp), Full (circular)
- Cards: Medium (12dp)
- Buttons: Full (20dp height / 2 = fully rounded)
- FAB: Large (16dp) or Full
- Navigation bar: Full pill indicator
- Text fields: Extra Small top corners (4dp), no bottom radius

---

## 4. Performance Gap Analysis

Based on: MS Learn "Improve app performance", MS Learn "Trim a .NET MAUI app", MVVM & DI patterns, additional MAUI resources.

### Current Performance Issues

| Issue | Severity | Current Code | Recommended Fix |
|---|---|---|---|
| **No Compiled Bindings** | 🔴 Critical | Manual `label.Text = value` assignments (no bindings at all) | Use `x:DataType` compiled bindings — 8-20x faster than reflection bindings. Since app is code-only, adopt `SetBinding()` with source-gen or switch to XAML. |
| **VerticalStackLayout + Children.Add loops** | 🔴 Critical | Every list page manually adds items via `foreach` → `Children.Add()` | Replace with `CollectionView` + `ItemTemplate` for virtualized scrolling — only creates visible items |
| **No Data Caching** | 🟡 High | Every `OnAppearing` calls full DB query via `LoadAsync()` | Add in-memory cache layer with TTL, invalidate on mutations |
| **All Pages Transient** | 🟡 High | `builder.Services.AddTransient<TPage>()` for all 24 pages | Keep frequently-visited pages (Dashboard, MoneyHub) as Singleton; use Transient for detail pages |
| **No Trimming Configuration** | 🟡 High | No TrimMode set in csproj | Enable `<TrimMode>full</TrimMode>` for release builds — significant APK size reduction |
| **No Image Optimization** | 🟠 Medium | No image caching strategy visible | Use `CacheStrategy="Disk"` on images, pre-scale images to display size |
| **No Lazy Initialization** | 🟠 Medium | All services registered eagerly | Use `Lazy<T>` for heavy services not needed immediately |
| **No Connection Pooling** | 🟠 Medium | SQLite DbContext per-request | Ensure single connection for SQLite (it's inherently single-writer) |
| **Grid vs StackLayout** | 🟠 Medium | Heavy use of VerticalStackLayout | Use Grid for complex layouts — avoids expensive measure passes |
| **OnAppearing Full Reload** | 🟠 Medium | Every page does full data reload on appearing | Track data version, skip reload if unchanged |
| **No IDisposable** | 🟡 Low | Pages don't implement IDisposable | Add cleanup for event subscriptions and timers |

### Recommended Build Configuration for Release

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <TrimMode>full</TrimMode>
    <EnableLLVM>true</EnableLLVM>
    <AndroidLinkTool>r8</AndroidLinkTool>
    <AndroidCreatePackagePerAbi>true</AndroidCreatePackagePerAbi>
    <PublishTrimmed>true</PublishTrimmed>
    <RunAOTCompilation>true</RunAOTCompilation>
    <DebugSymbols>false</DebugSymbols>
    <DebugType>none</DebugType>
</PropertyGroup>
```

### Performance Quick Wins

1. **Replace `VerticalStackLayout` loops with `CollectionView`** — biggest single impact for list-heavy pages (Transactions, Achievements, Subscriptions, Debt, etc.)
2. **Add `TrimMode=full`** — reduces APK size 30-50%
3. **Switch heavy pages off Transient** — avoid repeated page construction on tab switches
4. **Add loading indicators** — perceived performance improvement (skeleton screens during async load)
5. **Cache dashboard data** — dashboard is most-visited, currently does 5+ DB queries every OnAppearing

---

## 5. Architecture Gap Analysis

Based on: Enterprise Application Patterns Using .NET MAUI (MS Learn), MVVM best practices.

### Current Architecture

```
Pages (24 code-only C# files)
  └── Directly references IMobileFinanceService (injected via constructor DI)
  └── Manual UI construction in code-behind
  └── label.Text = value (no data binding)
  └── OnAppearing → LoadAsync pattern
```

### Recommended Architecture (per Enterprise Patterns)

```
ViewModels (INotifyPropertyChanged / ObservableObject)
  └── Commands (ICommand / RelayCommand / AsyncRelayCommand)
  └── ObservableCollection<T> for lists
  └── Calls Services via DI

Views (XAML or code with compiled bindings)
  └── BindingContext = ViewModel (via DI)
  └── Compiled data bindings to VM properties
  └── No business logic in code-behind

Services (same as current)
  └── IMobileFinanceService (already well-implemented)
  └── + INavigationService
  └── + ICacheService (new)
```

### MVVM Migration Strategy

Given the app has 24 code-only pages with no bindings, a **phased MVVM migration** is recommended:

**Phase 1 — Foundation** (do first):
- Add `CommunityToolkit.Mvvm` NuGet package
- Create `ViewModelBase` class extending `ObservableObject`
- Update DI registration pattern

**Phase 2 — Critical Pages** (highest traffic):
- `DashboardPage` → `DashboardViewModel` + compiled bindings
- `TransactionsPage` → `TransactionsViewModel` + `CollectionView`
- `BucketsPage` → `BucketsViewModel` + chart support

**Phase 3 — Remaining Pages** (progressive):
- Convert remaining pages as they need feature updates
- Each conversion: extract VM, add compiled bindings, add CollectionView where lists exist

### Pragmatic Note

Given the app is already code-only (no XAML), a full MVVM migration is a significant effort. A **pragmatic middle ground** would be:
1. Keep code-only pages BUT add `ObservableObject` ViewModels
2. Set `BindingContext` to VM, use `SetBinding()` in code for compiled bindings
3. Use `CollectionView` with `DataTemplate` (can be code-defined)
4. This gets 80% of the benefit with 40% of the effort

---

## 6. Prioritized Action Plan

### Priority Legend
- **P0** = Blocking release
- **P1** = Required for quality release
- **P2** = Strongly recommended
- **P3** = Nice to have / post-release

---

### P0 — Release Blockers

| # | Task | Est. Scope | Depends On |
|---|---|---|---|
| P0.1 | **M3 Color Token System** — Create semantic color tokens for light + dark themes (AppColors.cs rewrite) | 1 file | — |
| P0.2 | **Dark Mode** — Implement theme switching (light/dark/system) using color tokens | 3-5 files | P0.1 |
| P0.3 | **Replace hardcoded colors** — Audit all pages and replace inline colors with tokens | All 24 pages | P0.1 |
| P0.4 | **M3 Typography Scale** — Define 15 type styles mapped to Space Grotesk sizes/weights | 1 file | — |
| P0.5 | **Replace hardcoded font sizes** — Use type scale tokens throughout | All pages | P0.4 |
| P0.6 | **Release build config** — Add TrimMode, AOT, R8 linking to csproj | 1 file | — |
| P0.7 | **App identity** — Proper app name, icon, splash screen (not default purple .NET bot) | 3-4 files | — |

### P1 — Quality Release Requirements

| # | Task | Est. Scope | Depends On |
|---|---|---|---|
| P1.1 | **CollectionView migration** — Replace Children.Add() loops on list pages (Transactions, Achievements, Subscriptions, Debt, Goals, Challenges, FallbackHistory) | 7+ pages | — |
| P1.2 | **Charts library** — Add charting (Microcharts, LiveCharts2, or SkiaSharp custom) for Budget donut, Reports, Savings projection, Debt comparison | 1 NuGet + 4 pages | — |
| P1.3 | **Toast/Snackbar system** — Add CommunityToolkit.Maui Toast for mutations + game events (achievement unlocked, level up, XP gained) | 1 NuGet + service | — |
| P1.4 | **M3 Navigation Bar** — Restyle Shell TabBar with M3 pill indicator, proper icons, 80dp height | AppShell.cs + handlers | P0.1 |
| P1.5 | **M3 Card styles** — Create 3 card variants (Elevated, Filled, Outlined) with 12dp radius | Shared components | P0.1 |
| P1.6 | **M3 Button styles** — Implement 5 button types (Filled, Tonal, Elevated, Outlined, Text) | App.xaml + components | P0.1 |
| P1.7 | **Loading states** — Add skeleton/shimmer placeholders during async operations on all pages | All pages | — |
| P1.8 | **Empty states** — Add friendly illustrations + CTAs when lists are empty | All list pages | — |
| P1.9 | **Touch targets** — Audit and fix all interactive elements to minimum 48dp × 48dp | All pages | — |
| P1.10 | **Accessibility** — Add SemanticProperties.Description/Hint to all interactive elements, verify contrast ratios | All pages | P0.1 |

### P2 — Strongly Recommended

| # | Task | Est. Scope | Depends On |
|---|---|---|---|
| P2.1 | **Search & Filter** — Add search bar + category filter to Transactions and Subscriptions | 2 pages | P1.1 |
| P2.2 | **Game feedback** — Animated XP gain display, level-up celebration, streak celebration | GameLoop integration | P1.3 |
| P2.3 | **Dashboard gamification header** — Persistent level badge + XP progress bar | DashboardPage | P0.1, P0.4 |
| P2.4 | **FAB (Floating Action Button)** — Add M3 FAB on Transactions (add), Goals (new goal) | 2-3 pages | P0.1 |
| P2.5 | **Bottom sheets** — Use for transaction detail, filter panels, confirmations | Shared component | — |
| P2.6 | **Data caching layer** — In-memory cache with TTL for dashboard and frequently-accessed data | New service | — |
| P2.7 | **MVVM foundation** — Add CommunityToolkit.Mvvm, create ViewModelBase, convert Dashboard and Transactions pages | 2-3 pages + infra | — |
| P2.8 | **M3 Text Fields** — Restyle Entry/Editor to M3 filled/outlined variants | App.xaml | P0.1 |
| P2.9 | **Ripple effect** — Add Android native ripple to all tappable elements | Platform handler | — |
| P2.10 | **Page lifecycle optimization** — Track data version, skip reload when unchanged | Service layer | — |

### P3 — Post-Release / Nice to Have

| # | Task | Est. Scope | Depends On |
|---|---|---|---|
| P3.1 | **Animations** — Page transitions, card entry animations, number count-up animations | Various | — |
| P3.2 | **Onboarding flow** — First-run tutorial highlighting key features | New pages | — |
| P3.3 | **Export** — CSV/PDF export from Reports page | 1 page | — |
| P3.4 | **Android 12+ Dynamic Color** — Extract color from user wallpaper for Material You | Color system | P0.1, P0.2 |
| P3.5 | **Haptic feedback** — Vibration on key actions (add transaction, complete challenge) | Platform service | — |
| P3.6 | **Widget** — Android home screen widget showing balance/streak | Platform-specific | — |
| P3.7 | **Full MVVM migration** — Convert all remaining pages to MVVM pattern | Remaining pages | P2.7 |
| P3.8 | **Splash screen redesign** — Animated splash with logo → dashboard transition | Resources | — |
| P3.9 | **Chips** — M3 styled chips for category tags, transaction filters | Shared component | P0.1 |
| P3.10 | **Compiled bindings audit** — Ensure all bindings are compiled for trim safety | All pages | P2.7 |

---

## 7. Implementation Phases

### Phase 1: Foundation (Do First)

**Goal**: Establish M3 design system foundation and release build configuration.

1. Rewrite `AppColors.cs` → M3 color tokens (26+ roles, light + dark)
2. Create `M3Typography.cs` → 15 type style tokens
3. Create `M3Elevation.cs` → 5 elevation levels as tonal surfaces
4. Create `M3Shapes.cs` → Corner radius constants
5. Update `App.xaml` global styles to use new tokens
6. Add release build configuration to `.csproj`
7. Update app icon and splash screen
8. Implement theme detection + switching (light/dark/system)

### Phase 2: Core UX (Critical Path)

**Goal**: Fix the most impactful UX gaps for a quality Android experience.

1. Add `CommunityToolkit.Maui` NuGet (Toast, Snackbar, etc.)
2. Add charting library and implement budget donut + reports charts
3. Migrate top list pages to `CollectionView` (Transactions, Achievements, Subscriptions)
4. Add loading skeleton states to all pages
5. Add empty states to all list pages
6. Implement Toast/Snackbar for all create/update/delete operations
7. Add game feedback toasts (XP gain, level up, achievement unlock)

### Phase 3: M3 Components (Polish)

**Goal**: Apply M3 component styling throughout the app.

1. Restyle navigation bar (pill indicator, icons, 80dp height)
2. Implement M3 card variants across all pages
3. Implement M3 button variants (replace current flat buttons)
4. Add FAB to key pages
5. Restyle inputs (Entry/Editor) to M3 text fields
6. Add M3 interaction states (ripple, disabled opacity, focus rings)
7. Audit and fix all touch targets (48dp minimum)
8. Add SemanticProperties for accessibility

### Phase 4: Feature Completeness (Parity)

**Goal**: Close remaining feature gaps with Blazor.

1. Add search and filter to Transactions + Subscriptions
2. Add gamification header to Dashboard
3. Implement remaining charts (Savings projection, Debt comparison)
4. Add data caching layer
5. Optimize page lifecycle (skip unnecessary reloads)

### Phase 5: Release Prep

**Goal**: Final polish and Android-specific optimization.

1. Full testing pass on target Android devices
2. Performance profiling (startup time, navigation speed, memory)
3. Accessibility audit (TalkBack, contrast ratios)
4. APK size optimization (ensure trimming + R8 are working)
5. Android-specific polish (status bar color, navigation bar color, edge-to-edge)
6. Prepare Play Store listing (screenshots, description, privacy policy)

---

## Reference: M3 Color Token Template

For the color system implementation (P0.1), here are the required semantic tokens:

```
// Primary group
Primary, OnPrimary, PrimaryContainer, OnPrimaryContainer, PrimaryFixed, PrimaryFixedDim, OnPrimaryFixed, OnPrimaryFixedVariant

// Secondary group
Secondary, OnSecondary, SecondaryContainer, OnSecondaryContainer, SecondaryFixed, SecondaryFixedDim, OnSecondaryFixed, OnSecondaryFixedVariant

// Tertiary group
Tertiary, OnTertiary, TertiaryContainer, OnTertiaryContainer, TertiaryFixed, TertiaryFixedDim, OnTertiaryFixed, OnTertiaryFixedVariant

// Error group
Error, OnError, ErrorContainer, OnErrorContainer

// Surface group
Surface, OnSurface, OnSurfaceVariant, SurfaceContainerLowest, SurfaceContainerLow, SurfaceContainer, SurfaceContainerHigh, SurfaceContainerHighest, SurfaceDim, SurfaceBright, InverseSurface, InverseOnSurface, InversePrimary

// Outline
Outline, OutlineVariant

// Other
Scrim, Shadow
```

## Reference: M3 Typography Scale Template

```
Display  — Large (57/64), Medium (45/52), Small (36/44)
Headline — Large (32/40), Medium (28/36), Small (24/32)
Title    — Large (22/28), Medium (16/24 weight:500), Small (14/20 weight:500)
Body     — Large (16/24), Medium (14/20), Small (12/16)
Label    — Large (14/20 weight:500), Medium (12/16 weight:500), Small (11/16 weight:500)
```

Format: Size in sp / Line height in sp

## Note on 27-Tips PDF

The Syncfusion "27 Tips for Faster .NET MAUI Apps" ebook/PDF was not accessible online (page removed). The performance recommendations in this plan are sourced from:
- **Microsoft Learn**: "Improve .NET MAUI app performance" (compiled bindings, CollectionView, layout optimization, Shell, async/await)
- **Microsoft Learn**: "Trim a .NET MAUI app" (TrimMode, AOT, feature switches)
- **Enterprise Application Patterns**: MVVM, DI, navigation patterns
- **Medium MAUI best practices**: 9 tips (CollectionView, compiled bindings, weak references, lazy loading, IDisposable)
- **Envitics**: .NET MAUI best practices compilation

These sources together cover the same ground as the 27 Tips book (compiled bindings, layout choices, image caching, startup optimization, trimming, AOT, CollectionView, async patterns, DI container performance).
