using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Persistence;
using BriefMe.Infrastructure.Persistence.Interceptors;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Tests.Persistence;

/// <summary>
/// Pins the two halves of the SLA rule mapping (BRF-040) the domain tests cannot see: the value object and
/// the nullable enum surviving a round trip, and the scope uniqueness the aggregate cannot enforce on its own.
/// </summary>
public class SlaRulePersistenceTests
{
    private static AppDbContext CreateContext(string databaseName, string? currentUserId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor(new FakeCurrentUser(currentUserId)))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task SaveChanges_WhenRuleIsScoped_RoundTripsDeadlineAndPriority()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var categoryId = Guid.NewGuid();
        Guid ruleId;

        await using (var context = CreateContext(databaseName))
        {
            var rule = SlaRule.Create(categoryId, Priority.Urgent, 24);
            context.SlaRules.Add(rule);
            await context.SaveChangesAsync();
            ruleId = rule.Id;
        }

        // Act
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.SlaRules.FirstAsync(r => r.Id == ruleId);

        // Assert
        persisted.CategoryId.Should().Be(categoryId);
        persisted.Priority.Should().Be(Priority.Urgent);
        persisted.Deadline.Hours.Should().Be(24);
        persisted.Scope.Should().Be(SlaRuleScope.CategoryAndPriority);
    }

    [Fact]
    public async Task SaveChanges_WhenRuleIsGlobal_RoundTripsBothScopeColumnsAsNull()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        Guid ruleId;

        await using (var context = CreateContext(databaseName))
        {
            var rule = SlaRule.Create(null, null, 240);
            context.SlaRules.Add(rule);
            await context.SaveChangesAsync();
            ruleId = rule.Id;
        }

        // Act
        await using var readContext = CreateContext(databaseName);
        var persisted = await readContext.SlaRules.FirstAsync(r => r.Id == ruleId);

        // Assert
        persisted.CategoryId.Should().BeNull();
        persisted.Priority.Should().BeNull();
        persisted.Scope.Should().Be(SlaRuleScope.Global);
    }

    [Fact]
    public async Task SaveChanges_WhenRuleIsAdded_StampsAuditColumnsThroughTheInterceptor()
    {
        // Arrange
        const string administrator = "admin@briefme.com";
        await using var context = CreateContext(Guid.NewGuid().ToString(), administrator);
        var rule = SlaRule.Create(null, Priority.High, 48);
        context.SlaRules.Add(rule);

        // Act
        await context.SaveChangesAsync();

        // Assert
        rule.CreatedAt.Should().NotBe(default);
        rule.CreatedBy.Should().Be(administrator);
    }

    [Fact]
    public void Model_WhenBuilt_MapsTheDeadlineValueObjectToAnHoursColumn()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());

        // Act
        var deadline = context.Model
            .FindEntityType(typeof(SlaRule))!
            .FindProperty(nameof(SlaRule.Deadline))!;

        // Assert
        deadline.GetColumnName().Should().Be("DeadlineHours");
        deadline.ClrType.Should().Be<Domain.ValueObjects.SlaDeadline>();
        deadline.GetValueConverter()!.ProviderClrType.Should().Be<int>();
    }

    [Fact]
    public void Model_WhenBuilt_DeclaresTheScopeIndexAsUniqueWithNullsNotDistinct()
    {
        // Arrange
        using var context = CreateContext(Guid.NewGuid().ToString());

        // Act
        var index = context.Model
            .FindEntityType(typeof(SlaRule))!
            .GetIndexes()
            .Single();

        // Assert
        index.GetDatabaseName().Should().Be("UX_SlaRules_CategoryId_Priority");
        index.IsUnique.Should().BeTrue();
        index.Properties.Select(p => p.Name).Should()
            .Equal(nameof(SlaRule.CategoryId), nameof(SlaRule.Priority));

        // Without this, PostgreSQL counts every NULL as its own value and any number of global rules —
        // (NULL, NULL) — would pass the unique index.
        index.GetAreNullsDistinct().Should().BeFalse();
    }
}
