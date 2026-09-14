using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Sla.MonitorDeadlines;
using BriefMe.Domain.Common;
using BriefMe.Infrastructure.Sla;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BriefMe.Infrastructure.Tests.Sla;

/// <summary>
/// Pins the scheduling half of the deadline monitor (RF028, BRF-043): that it sweeps on the configured
/// interval, that it can be switched off, and that a bad pass costs one pass instead of the whole service.
/// </summary>
/// <remarks>
/// Driven by <see cref="FakeTimeProvider"/>: the service builds its delay and its
/// <see cref="PeriodicTimer"/> over the injected provider, so fifteen minutes pass here in microseconds.
/// </remarks>
public class SlaMonitorBackgroundServiceTests
{
    private static readonly SlaMonitorRunDto EmptyRun = new(DateTime.UtcNow, 0, 0, 0, 0);

    /// <summary>Counts the passes and, optionally, fails the first ones.</summary>
    private sealed class CountingSender(int failuresBeforeSuccess = 0) : ISender
    {
        private int _calls;

        public int Passes => _calls;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            request.Should().BeOfType<MonitorDeadlinesCommand>();

            if (Interlocked.Increment(ref _calls) <= failuresBeforeSuccess)
                throw new InvalidOperationException("Database unreachable.");

            return Task.FromResult((TResponse)(object)Result.Success(EmptyRun));
        }
    }

    private static (SlaMonitorBackgroundService Service, CountingSender Sender) CreateService(
        FakeTimeProvider timeProvider,
        SlaMonitorSettings settings,
        int failuresBeforeSuccess = 0)
    {
        var sender = new CountingSender(failuresBeforeSuccess);

        var services = new ServiceCollection();
        services.AddScoped<ISender>(_ => sender);

        var service = new SlaMonitorBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            timeProvider,
            NullLogger<SlaMonitorBackgroundService>.Instance);

        return (service, sender);
    }

    /// <summary>
    /// Lets the service reach its next wait before the clock is moved. <c>StartAsync</c> returns at the first
    /// await, so a timer advanced too early is advanced past a waiter that does not exist yet.
    /// </summary>
    private static async Task SettleAsync()
    {
        for (var i = 0; i < 10; i++)
            await Task.Yield();

        await Task.Delay(25);
    }

    /// <summary>Polls until <paramref name="condition"/> holds, or gives up — the pass is asynchronous.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10);
    }

    /// <summary>Settles, moves the fake clock, then waits for the pass that tick was expected to trigger.</summary>
    private static async Task AdvanceAsync(
        FakeTimeProvider timeProvider,
        TimeSpan delay,
        CountingSender? sender = null,
        int expectedPasses = 0)
    {
        await SettleAsync();
        timeProvider.Advance(delay);

        if (sender is not null)
            await WaitUntilAsync(() => sender.Passes >= expectedPasses);
        else
            await SettleAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_NeverSweeps()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var (service, sender) = CreateService(
            timeProvider,
            new SlaMonitorSettings { Enabled = false, StartupDelaySeconds = 0, IntervalMinutes = 1 });

        // Act
        await service.StartAsync(CancellationToken.None);
        await AdvanceAsync(timeProvider, TimeSpan.FromMinutes(10));
        await service.StopAsync(CancellationToken.None);

        // Assert
        sender.Passes.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStartupDelayElapses_RunsTheFirstPass()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var (service, sender) = CreateService(
            timeProvider,
            new SlaMonitorSettings { StartupDelaySeconds = 30, IntervalMinutes = 15 });

        // Act
        await service.StartAsync(CancellationToken.None);
        await AdvanceAsync(timeProvider, TimeSpan.FromSeconds(30), sender, expectedPasses: 1);
        await service.StopAsync(CancellationToken.None);

        // Assert
        sender.Passes.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_BeforeTheStartupDelayElapses_HasNotSweptYet()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var (service, sender) = CreateService(
            timeProvider,
            new SlaMonitorSettings { StartupDelaySeconds = 30, IntervalMinutes = 15 });

        // Act
        await service.StartAsync(CancellationToken.None);
        await AdvanceAsync(timeProvider, TimeSpan.FromSeconds(20));
        await service.StopAsync(CancellationToken.None);

        // Assert
        sender.Passes.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSeveralIntervalsElapse_SweepsOncePerInterval()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var (service, sender) = CreateService(
            timeProvider,
            new SlaMonitorSettings { StartupDelaySeconds = 0, IntervalMinutes = 15 });

        // Act
        await service.StartAsync(CancellationToken.None);
        await AdvanceAsync(timeProvider, TimeSpan.Zero, sender, expectedPasses: 1);
        await AdvanceAsync(timeProvider, TimeSpan.FromMinutes(15), sender, expectedPasses: 2);
        await AdvanceAsync(timeProvider, TimeSpan.FromMinutes(15), sender, expectedPasses: 3);
        await service.StopAsync(CancellationToken.None);

        // Assert
        sender.Passes.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAPassThrows_KeepsSweepingOnTheNextInterval()
    {
        // Arrange
        // A BackgroundService that lets an exception escape ends silently; the monitor would simply stop
        // existing, with the host still up.
        var timeProvider = new FakeTimeProvider();
        var (service, sender) = CreateService(
            timeProvider,
            new SlaMonitorSettings { StartupDelaySeconds = 0, IntervalMinutes = 15 },
            failuresBeforeSuccess: 1);

        // Act
        await service.StartAsync(CancellationToken.None);
        await AdvanceAsync(timeProvider, TimeSpan.Zero, sender, expectedPasses: 1);
        await AdvanceAsync(timeProvider, TimeSpan.FromMinutes(15), sender, expectedPasses: 2);
        await service.StopAsync(CancellationToken.None);

        // Assert
        sender.Passes.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStopped_CompletesWithoutFaulting()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var (service, sender) = CreateService(
            timeProvider,
            new SlaMonitorSettings { StartupDelaySeconds = 0, IntervalMinutes = 15 });

        // Act
        await service.StartAsync(CancellationToken.None);
        await AdvanceAsync(timeProvider, TimeSpan.Zero, sender, expectedPasses: 1);
        await service.StopAsync(CancellationToken.None);

        // Assert
        service.ExecuteTask!.IsFaulted.Should().BeFalse();
    }
}
