using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Sla.MonitorDeadlines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Sla;

/// <summary>
/// Runs the deadline sweep on a timer (RF028, BRF-043). Scheduling only: which demands are late, what gets
/// marked and what gets published is <see cref="MonitorDeadlinesCommandHandler"/>'s — an
/// <see cref="IHostedService"/> holds no business rule, exactly as the Web layer holds none.
/// </summary>
/// <remarks>
/// Three things this class exists to get right:
/// <list type="number">
/// <item>
/// A scope per pass. Hosted services are singletons while <c>AppDbContext</c>, the repositories and
/// <c>IUnitOfWork</c> are scoped, so capturing <see cref="ISender"/> in the constructor would pin one
/// <c>DbContext</c> for the life of the process.
/// </item>
/// <item>
/// No exception escapes <see cref="ExecuteAsync"/>. A <see cref="BackgroundService"/> that throws ends
/// silently: the host stays up and the monitoring simply stops existing, with nothing to show for it.
/// </item>
/// <item>
/// A regular tick. <see cref="PeriodicTimer"/> keeps the interval steady even when a pass runs long, and —
/// built over the injected <see cref="TimeProvider"/> — lets a test drive fifteen minutes without waiting
/// them.
/// </item>
/// </list>
/// </remarks>
public sealed class SlaMonitorBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaMonitorSettings> options,
    TimeProvider timeProvider,
    ILogger<SlaMonitorBackgroundService> logger) : BackgroundService
{
    private readonly SlaMonitorSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("SLA deadline monitor is disabled; no deadline will be evaluated.");
            return;
        }

        var interval = TimeSpan.FromMinutes(_settings.IntervalMinutes);

        logger.LogInformation(
            "SLA deadline monitor starting; first pass in {StartupDelaySeconds}s, then every {Interval}.",
            _settings.StartupDelaySeconds,
            interval);

        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(_settings.StartupDelaySeconds),
                timeProvider,
                stoppingToken);

            using var timer = new PeriodicTimer(interval, timeProvider);

            // do/while, so the first pass runs at the end of the startup delay rather than one full
            // interval later.
            do
            {
                await RunPassAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is shutting down; this is the ordinary way out of the loop.
        }

        logger.LogInformation("SLA deadline monitor stopped.");
    }

    private async Task RunPassAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(
                new MonitorDeadlinesCommand(_settings.BatchSize, _settings.MaxItemsPerRun),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning("SLA deadline pass reported a failure: {Error}", result.Error);
                return;
            }

            var run = result.Value;

            logger.LogInformation(
                "SLA deadline pass at {EvaluatedAtUtc:O}: {TasksMarked}/{TasksScanned} tasks and "
                + "{BriefingsMarked}/{BriefingsScanned} briefings marked overdue.",
                run.EvaluatedAtUtc,
                run.TasksMarkedOverdue,
                run.TasksScanned,
                run.BriefingsMarkedOverdue,
                run.BriefingsScanned);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A transient database or dispatch failure costs this pass, not the monitor: the next tick
            // sweeps the same demands again, since nothing was marked.
            logger.LogError(exception, "SLA deadline pass failed; the next pass runs on schedule.");
        }
    }
}
