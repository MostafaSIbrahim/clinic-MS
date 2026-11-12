using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Startup;

/// <summary>
/// P9b: marks reservations that were never checked in, 24 hours after their appointment time, as No show.
/// Runs shortly after the app starts and then every hour while it runs. On shared hosting the app may sleep when
/// nobody uses it; the first request wakes it, and the sweep runs again then.
/// </summary>
public sealed class NoShowSweeper : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NoShowSweeper> _log;

    public NoShowSweeper(IServiceScopeFactory scopes, ILogger<NoShowSweeper> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);   // let start-up (migrations, seeding) finish
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await SweepAsync();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // the app is stopping
        }
    }

    private async Task SweepAsync()
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var changed = await scope.ServiceProvider.GetRequiredService<IReservationService>().MarkMissedAsNoShowAsync();
            if (changed > 0)
                _log.LogInformation("Marked {Count} reservation(s) not checked in within 24 hours as No show.", changed);
        }
        catch (Exception ex)
        {
            // Never stop the app over this; the next run tries again.
            _log.LogWarning(ex, "The no-show sweep failed; it will run again in an hour.");
        }
    }
}
