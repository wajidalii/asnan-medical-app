using Asnan.Application.Reminders;
using Microsoft.Extensions.Options;

namespace Asnan.Api.BackgroundServices;

/// <summary>
/// Periodically scans for due appointment reminders — issue #25. Lives in
/// Asnan.Api (not Infrastructure) purely because that's where the ASP.NET
/// Core hosting framework reference already is; the actual scheduling/dedup
/// logic it delegates to (<see cref="IReminderSchedulingService"/>) is
/// framework-agnostic and independently unit-tested.
/// </summary>
public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private readonly ReminderOptions _options;

    public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger, IOptions<ReminderOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.ScanIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay first: avoids every short-lived host (integration tests
            // spin up a full WebApplicationFactory<Program> per test class)
            // racing an eager scan against whatever the test is mid-arranging.
            // Inconsequential in production — one interval's worth of latency
            // before the very first scan.
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var schedulingService = scope.ServiceProvider.GetRequiredService<IReminderSchedulingService>();
                var sentCount = await schedulingService.ScanAndSendDueRemindersAsync(DateTime.UtcNow, stoppingToken);
                if (sentCount > 0)
                {
                    _logger.LogInformation("Sent {SentCount} appointment reminder(s).", sentCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reminder scan failed — will retry on the next interval.");
            }
        }
    }
}
