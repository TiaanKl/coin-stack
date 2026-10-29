using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoinStack.Services;

/// <summary>
/// Runs the daily catch-up when the web app starts and again just after each local midnight while it
/// keeps running. It also looks in hourly, so a computer that slept through midnight catches up soon
/// after it wakes.
/// </summary>
public sealed class DailyCatchUpHostedService : BackgroundService
{
    private readonly IDailyCatchUpService _catchUp;
    private readonly TimeProvider _clock;
    private readonly ILogger<DailyCatchUpHostedService> _logger;

    public DailyCatchUpHostedService(IDailyCatchUpService catchUp, TimeProvider clock, ILogger<DailyCatchUpHostedService> logger)
    {
        _catchUp = catchUp;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _catchUp.RunIfDueAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The daily catch-up failed; it will try again later.");
            }

            var nextDay = _clock.StartOfDayUtc(_clock.Today().AddDays(1)) - _clock.GetUtcNow().UtcDateTime + TimeSpan.FromMinutes(1);
            var wait = nextDay < TimeSpan.FromHours(1) ? nextDay : TimeSpan.FromHours(1);
            await Task.Delay(wait, _clock, stoppingToken);
        }
    }
}
