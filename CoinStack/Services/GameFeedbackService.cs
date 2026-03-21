namespace CoinStack.Services;

public sealed class GameFeedbackService : IGameFeedbackService
{
    public event Action<FeedbackPayload>? OnFeedback;

    public void Trigger(FeedbackPayload payload) => OnFeedback?.Invoke(payload);
}
