using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BriefMe.Infrastructure.Tests.Messaging;

/// <summary>
/// Pins the delivery half of the publishing pipeline (BRF-043; generalized by BRF-049): who receives an
/// event, that events are not re-delivered, and that one failing reaction does not silence the rest.
/// </summary>
public class DomainEventDispatcherTests
{
    private static readonly DateTime DueAtUtc = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DetectedAtUtc = new(2026, 8, 2, 0, 15, 0, DateTimeKind.Utc);

    private sealed class RecordingHandler(List<string> log) : IDomainEventHandler<DeadlineOverdue>
    {
        public Task Handle(DeadlineOverdue domainEvent, CancellationToken cancellationToken)
        {
            log.Add($"recorded:{domainEvent.SubjectId}");
            return Task.CompletedTask;
        }
    }

    /// <summary>Records that it was reached, then fails — so a swallowed failure is still observable.</summary>
    private sealed class ThrowingHandler(List<string> log) : IDomainEventHandler<DeadlineOverdue>
    {
        public Task Handle(DeadlineOverdue domainEvent, CancellationToken cancellationToken)
        {
            log.Add($"attempted:{domainEvent.SubjectId}");
            throw new InvalidOperationException("Notification service is down.");
        }
    }

    private sealed class OtherEventHandler(List<string> log) : IDomainEventHandler<TaskAssigned>
    {
        public Task Handle(TaskAssigned domainEvent, CancellationToken cancellationToken)
        {
            log.Add("wrong-handler");
            return Task.CompletedTask;
        }
    }

    private static DomainEventDispatcher CreateDispatcher(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);

        return new DomainEventDispatcher(services.BuildServiceProvider(), NullLogger<DomainEventDispatcher>.Instance);
    }

    /// <summary>A task already marked overdue, therefore carrying one pending <see cref="DeadlineOverdue"/>.</summary>
    private static BriefingTask MarkedTask()
    {
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium, Guid.NewGuid());
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        task.ClearDomainEvents();
        task.MarkOverdue(DueAtUtc, DetectedAtUtc);
        return task;
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerIsRegistered_DeliversTheEventToIt()
    {
        // Arrange
        var log = new List<string>();
        var dispatcher = CreateDispatcher(services =>
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new RecordingHandler(log)));
        var task = MarkedTask();

        // Act
        await dispatcher.DispatchAsync([task]);

        // Assert
        log.Should().Equal($"recorded:{task.Id}");
    }

    [Fact]
    public async Task DispatchAsync_WhenSeveralHandlersAreRegistered_DeliversToAllOfThem()
    {
        // Arrange
        var log = new List<string>();
        var dispatcher = CreateDispatcher(services =>
        {
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new RecordingHandler(log));
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new RecordingHandler(log));
        });

        // Act
        await dispatcher.DispatchAsync([MarkedTask()]);

        // Assert
        log.Should().HaveCount(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlersOfOtherEventsExist_LeavesThemAlone()
    {
        // Arrange
        var log = new List<string>();
        var dispatcher = CreateDispatcher(services =>
            services.AddScoped<IDomainEventHandler<TaskAssigned>>(_ => new OtherEventHandler(log)));

        // Act
        await dispatcher.DispatchAsync([MarkedTask()]);

        // Assert
        log.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenNoHandlerIsRegistered_CompletesWithoutFailing()
    {
        // Arrange
        // The state of DeadlineOverdue until BRF-053 arrives.
        var dispatcher = CreateDispatcher(_ => { });

        // Act
        var act = () => dispatcher.DispatchAsync([MarkedTask()]);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_WhenTheEventsAreDelivered_ClearsThemFromTheAggregate()
    {
        // Arrange
        var dispatcher = CreateDispatcher(_ => { });
        var task = MarkedTask();

        // Act
        await dispatcher.DispatchAsync([task]);

        // Assert
        task.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenCalledTwice_DoesNotRedeliverTheSameEvent()
    {
        // Arrange
        var log = new List<string>();
        var dispatcher = CreateDispatcher(services =>
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new RecordingHandler(log)));
        var task = MarkedTask();

        // Act
        await dispatcher.DispatchAsync([task]);
        await dispatcher.DispatchAsync([task]);

        // Assert
        log.Should().HaveCount(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerThrows_DoesNotPropagate()
    {
        // Arrange
        // The mark is already committed; a failed reaction cannot undo it, so it must not abort the pass.
        var dispatcher = CreateDispatcher(services =>
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new ThrowingHandler([])));

        // Act
        var act = () => dispatcher.DispatchAsync([MarkedTask()]);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_WhenOneEventFails_StillDeliversTheFollowingOnes()
    {
        // Arrange
        var log = new List<string>();
        var dispatcher = CreateDispatcher(services =>
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new ThrowingHandler(log)));

        // Act
        await dispatcher.DispatchAsync([MarkedTask(), MarkedTask()]);

        // Assert
        log.Should().HaveCount(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenAnAggregateHasNoPendingEvent_CostsNothing()
    {
        // Arrange
        var log = new List<string>();
        var dispatcher = CreateDispatcher(services =>
            services.AddScoped<IDomainEventHandler<DeadlineOverdue>>(_ => new RecordingHandler(log)));
        var untouched = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium);

        // Act
        await dispatcher.DispatchAsync([(AggregateRoot)untouched]);

        // Assert
        log.Should().BeEmpty();
    }
}
