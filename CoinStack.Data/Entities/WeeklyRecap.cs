namespace CoinStack.Data.Entities;

public sealed class WeeklyRecap : EntityBase
{
    public int WeekNumber { get; set; }

    public int Year { get; set; }

    public decimal TotalSpent { get; set; }

    public decimal TotalIncome { get; set; }

    public decimal TotalSaved { get; set; }

    public int PointsEarned { get; set; }

    public int ChallengesCompleted { get; set; }

    public int ReflectionsCompleted { get; set; }

    public int StreakDays { get; set; }

    public string TopCategory { get; set; } = "";

    public string InsightMessage { get; set; } = "";

    public bool IsViewed { get; set; }
}
