using System;

namespace CoinStack.Desktop.ViewModels;

public sealed class TransactionRow
{
    public int Id { get; init; }
    public DateTime Date { get; init; }
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string TypeLabel { get; init; } = "";
    public string AmountText { get; init; } = "";
    public bool IsIncome { get; init; }
    public string DateText => Date.ToLocalTime().ToString("MMM dd, yyyy");
}

public sealed class ScoreEventRow
{
    public string Description { get; init; } = "";
    public string PointsText { get; init; } = "";
    public bool IsPositive { get; init; }
    public string DateText { get; init; } = "";
}

public sealed class BucketRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string CategoryText { get; init; } = "";
    public string? GoalText { get; init; }
    public string AllocatedText { get; init; } = "";
    public string SpentText { get; init; } = "";
    public string RemainingText { get; init; } = "";
    public string ProgressPercentText { get; init; } = "";
    public double ProgressValue { get; init; }
    public double ProgressMaximum { get; init; } = 100;
    public bool IsOverBudget { get; init; }
    public bool IsSavings { get; init; }
    public string StatusText { get; init; } = "";
}

public sealed class GoalRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string CurrentText { get; init; } = "";
    public string TargetText { get; init; } = "";
    public string? TargetDateText { get; init; }
    public string StatusText { get; init; } = "";
    public double ProgressValue { get; init; }
    public double ProgressMaximum { get; init; } = 100;
    public bool IsCompleted { get; init; }
}

public sealed class DebtRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Provider { get; init; } = "";
    public string BalanceText { get; init; } = "";
    public string TotalText { get; init; } = "";
    public string InterestText { get; init; } = "";
    public string PaymentText { get; init; } = "";
    public string ProgressPercentText { get; init; } = "";
    public double ProgressValue { get; init; }
    public double ProgressMaximum { get; init; } = 100;
}

public sealed class CategoryRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string ScopeText { get; init; } = "";
}

public sealed class SubscriptionRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string CycleText { get; init; } = "";
    public string CostText { get; init; } = "";
    public string StatusText { get; init; } = "";
    public string DebitDayText { get; init; } = "";
}

public sealed record CategoryOption(int Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record GoalOption(int Id, string Name)
{
    public override string ToString() => Name;
}
