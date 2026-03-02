namespace CoinStack.Data.Entities;

public sealed class ScoreEvent : EntityBase
{
    public int Points { get; set; }

    public ScoreChangeReason Reason { get; set; }

    public string Description { get; set; } = "";

    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public int? BucketId { get; set; }
    public Bucket? Bucket { get; set; }
}
