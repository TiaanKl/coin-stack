namespace CoinStack.Services;

public sealed record FeedbackPayload(
    int PointsChanged,
    string Message,
    FeedbackKind Kind);

public interface IGameFeedbackService
{
    event Action<FeedbackPayload>? OnFeedback;
    void Trigger(FeedbackPayload payload);
}
