using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using BriefMe.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

public class UnitOfWorkTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CommitAsync_WhenMultipleEntitiesAreAdded_PersistsAllAtomically()
    {
        // Arrange
        await using var context = CreateContext();
        var unitOfWork = new UnitOfWork(context);
        context.Users.Add(User.Create("Ana", Email.Create("ana@empresa.com").Value, "hash"));
        context.Users.Add(User.Create("Bia", Email.Create("bia@empresa.com").Value, "hash"));

        // Act
        await unitOfWork.CommitAsync();

        // Assert
        (await context.Users.CountAsync()).Should().Be(2);
    }
}
