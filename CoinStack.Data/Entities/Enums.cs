namespace CoinStack.Data.Entities;

public enum TransactionType
{
    Expense = 0,
    Income = 1,
    Transfer = 2
}

public enum CategoryScope
{
    Both = 0,
    Expense = 1,
    Income = 2,
    Transfer = 3
}

public enum ExpenseKind
{
    Discretionary = 0,
    Mandatory = 1,
    ForceMajeure = 2
}

public enum SubscriptionCycle
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}

public enum SubscriptionStatus
{
    Active = 0,
    Paused = 1,
    Cancelled = 2
}

public enum GoalStatus
{
    Active = 0,
    Completed = 1,
    Abandoned = 2
}

public enum ScoreChangeReason
{
    UnderBudget = 0,
    OverBudget = 1,
    SavingsDip = 2,
    StreakBonus = 3,
    ReflectionCompleted = 4,
    DailyCheckIn = 5,
    ImpulseResisted = 6,
    ManualAdjustment = 7,
    GoalAchieved = 8,
    ImpulseBuy = 9,
    ForceMajeure = 10,
    EmergencyFundDip = 11
}

public enum StreakType
{
    DailyUnderBudget = 0,
    WeeklyUnderBudget = 1,
    NoImpulseBuy = 2,
    DailyCheckIn = 3
}

public enum ReflectionTrigger
{
    OverBudgetSpend = 0,
    SavingsDip = 1,
    LargeExpense = 2,
    ImpulseBuy = 3,
    ManualEntry = 4
}

public enum EmotionTag
{
    Neutral = 0,
    Proud = 1,
    Stressed = 2,
    Anxious = 3,
    Impulsive = 4,
    Tired = 5,
    Bored = 6,
    Excited = 7,
    Guilty = 8,
    Motivated = 9
}

public enum WaitlistPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum CoolOffPeriod
{
    Hours24 = 0,
    Days3 = 1,
    Days7 = 2,
    Days30 = 3
}
