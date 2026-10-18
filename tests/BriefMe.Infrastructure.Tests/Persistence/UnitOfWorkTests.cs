using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

public class UnitOfWorkTests
{
    private static AppDbContext CreateContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CommitAsync_WhenMultipleEntitiesAreAdded_PersistsAllAtomically()
    {
        // Arrange
        await using var context = CreateContext();
        var unitOfWork = new UnitOfWork(context);
        context.Users.Add(User.Create("Ana", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester));
        context.Users.Add(User.Create("Bia", Email.Create("bia@empresa.com").Value, "hash", UserRole.Requester));

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        (await context.Users.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CommitAsync_WhenTheRowChangedUnderneath_ThrowsConcurrencyConflict()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = User.Create("Ana", Email.Create("ana@empresa.com").Value, "hash", UserRole.Requester);
        await using (var seed = CreateContext(databaseName))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        await using var first = CreateContext(databaseName);
        await using var second = CreateContext(databaseName);
        var stale = await second.Users.SingleAsync();
        first.Users.Remove(await first.Users.SingleAsync());
        await first.SaveChangesAsync();
        stale.ChangePassword("new-hash");

        // Act
        var act = () => new UnitOfWork(second).CommitAsync();

        // Assert
        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }
}
