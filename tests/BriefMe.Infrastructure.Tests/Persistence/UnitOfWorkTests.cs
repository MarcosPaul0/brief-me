using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Events;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

/// <summary>
/// Pins the publishing pipeline of BRF-049: committing is what publishes, it publishes after the write, and
/// it publishes each event once. Who receives an event is <c>DomainEventDispatcherTests</c>.
/// </summary>
public class UnitOfWorkTests
{
    private static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>A tracked task carrying one pending <see cref="TaskAssigned"/>.</summary>
    private static async Task<BriefingTask> TrackedAssignedTaskAsync(AppDbContext context)
    {
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium);
        await context.Tasks.AddAsync(task);
        await context.SaveChangesAsync();
        task.ClearDomainEvents();

        task.AssignTo(Guid.NewGuid(), Guid.NewGuid());
        return task;
    }

    [Fact]
    public async Task CommitAsync_WhenMultipleEntitiesAreAdded_PersistsAllAtomically()
    {
        // Arrange
        await using var context = CreateContext();
        var unitOfWork = new UnitOfWork(context, new RecordingDomainEventDispatcher());
        context.Permissions.Add(Permission.Create("users.create", "Criar usuários", null, "Usuários"));
        context.Permissions.Add(Permission.Create("users.delete", "Excluir usuários", null, "Usuários"));

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        (await context.Permissions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CommitAsync_WhenATrackedAggregateHasAPendingEvent_PublishesIt()
    {
        // Arrange
        await using var context = CreateContext();
        var dispatcher = new RecordingDomainEventDispatcher();
        var unitOfWork = new UnitOfWork(context, dispatcher);
        var task = await TrackedAssignedTaskAsync(context);

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        dispatcher.DispatchedEvents.OfType<TaskAssigned>().Single().TaskId.Should().Be(task.Id);
    }

    [Fact]
    public async Task CommitAsync_WhenTwoAggregatesRaiseEvents_PublishesBoth()
    {
        // Arrange
        await using var context = CreateContext();
        var dispatcher = new RecordingDomainEventDispatcher();
        var unitOfWork = new UnitOfWork(context, dispatcher);
        var first = await TrackedAssignedTaskAsync(context);
        var second = await TrackedAssignedTaskAsync(context);

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        dispatcher.DispatchedEvents.OfType<TaskAssigned>().Select(e => e.TaskId)
            .Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task CommitAsync_WhenPublishing_TheChangeIsAlreadyPersisted()
    {
        // Arrange
        await using var context = CreateContext();
        var designerId = Guid.NewGuid();
        Guid? designerSeenByHandler = null;

        // Reads with AsNoTracking so the answer comes from the store, not from the instance the use case
        // mutated: a handler that runs before the write would see no row at all.
        var dispatcher = new RecordingDomainEventDispatcher(async events =>
        {
            var taskId = events.OfType<TaskAssigned>().Single().TaskId;
            var persisted = await context.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
            designerSeenByHandler = persisted.DesignerId;
        });

        var unitOfWork = new UnitOfWork(context, dispatcher);
        var task = BriefingTask.Create(Guid.NewGuid(), "Key visual", Priority.Medium);
        await context.Tasks.AddAsync(task);
        task.ClearDomainEvents();
        task.AssignTo(designerId, Guid.NewGuid());

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        designerSeenByHandler.Should().Be(designerId);
    }

    [Fact]
    public async Task CommitAsync_WhenNoAggregateRaisedAnything_DoesNotCallTheDispatcher()
    {
        // Arrange
        await using var context = CreateContext();
        var dispatcher = new RecordingDomainEventDispatcher();
        var unitOfWork = new UnitOfWork(context, dispatcher);
        context.Permissions.Add(Permission.Create("users.create", "Criar usuários", null, "Usuários"));

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        dispatcher.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CommitAsync_WhenTheSameAggregateIsCommittedTwice_PublishesTheEventOnce()
    {
        // Arrange
        await using var context = CreateContext();
        var dispatcher = new RecordingDomainEventDispatcher();
        var unitOfWork = new UnitOfWork(context, dispatcher);
        var task = await TrackedAssignedTaskAsync(context);
        await unitOfWork.CommitAsync();

        // Act
        task.UpdateStatus(BriefingTaskStatus.InProgress);
        await unitOfWork.CommitAsync();

        // Assert
        dispatcher.DispatchedEvents.OfType<TaskAssigned>().Should().ContainSingle();
    }

    [Fact]
    public async Task CommitAsync_WhenTheAggregateIsDeletedInTheSameCommit_StillPublishesItsEvents()
    {
        // Arrange
        await using var context = CreateContext();
        var dispatcher = new RecordingDomainEventDispatcher();
        var unitOfWork = new UnitOfWork(context, dispatcher);
        var task = await TrackedAssignedTaskAsync(context);
        context.Tasks.Remove(task);

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        dispatcher.DispatchedEvents.Should().ContainSingle(e => e is TaskAssigned);
    }

    [Fact]
    public async Task CommitAsync_WhenTheWriteFails_PublishesNothing()
    {
        // Arrange
        await using var context = CreateContext();
        var dispatcher = new RecordingDomainEventDispatcher();
        var unitOfWork = new UnitOfWork(context, dispatcher);
        await TrackedAssignedTaskAsync(context);
        var cancelled = new CancellationToken(canceled: true);

        // Act
        var act = () => unitOfWork.CommitAsync(cancelled);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        dispatcher.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CommitAsync_WhenAHandlerCommitsAgain_TheNestedPassPublishesOnlyWhatItRaised()
    {
        // Arrange
        await using var context = CreateContext();
        UnitOfWork? unitOfWork = null;
        var passes = new List<IReadOnlyCollection<IDomainEvent>>();

        var reacted = BriefingTask.Create(Guid.NewGuid(), "Roteiro", Priority.Medium);
        await context.Tasks.AddAsync(reacted);
        await context.SaveChangesAsync();
        reacted.ClearDomainEvents();

        // Stands in for CompleteBriefingWhenAllTasksApprovedHandler: reacts by changing another aggregate and
        // committing from inside the dispatch of the commit that woke it.
        var dispatcher = new RecordingDomainEventDispatcher(async events =>
        {
            passes.Add([.. events]);

            if (events.Any(e => e is TaskAssigned))
            {
                reacted.UpdateStatus(BriefingTaskStatus.InProgress);
                await unitOfWork!.CommitAsync();
            }
        });

        unitOfWork = new UnitOfWork(context, dispatcher);
        await TrackedAssignedTaskAsync(context);

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        passes.Should().HaveCount(2);
        passes[0].Should().ContainSingle(e => e is TaskAssigned);
        passes[1].Should().ContainSingle(e => e is EntityChanged);
    }
}
