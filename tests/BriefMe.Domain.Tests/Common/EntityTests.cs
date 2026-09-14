using BriefMe.Domain.Common;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Common;

public class EntityTests
{
    private sealed class TestEntity : Entity
    {
        public TestEntity(Guid id) : base(id)
        {
        }
    }

    private sealed class SoftDeletableTestEntity : Entity, ISoftDelete
    {
        public SoftDeletableTestEntity(Guid id) : base(id)
        {
        }

        public bool IsDeleted { get; private set; }

        public DateTime? DeletedAt { get; private set; }

        public string? DeletedBy { get; private set; }

        public void MarkAsDeleted(string? deletedBy)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedBy = deletedBy;
        }
    }

    [Fact]
    public void Equals_WhenSameIdAndType_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var first = new TestEntity(id);
        var second = new TestEntity(id);

        // Act
        var result = first.Equals(second);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentId_ReturnsFalse()
    {
        // Arrange
        var first = new TestEntity(Guid.NewGuid());
        var second = new TestEntity(Guid.NewGuid());

        // Act
        var result = first.Equals(second);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MarkAsDeleted_WhenCalled_SetsSoftDeleteFields()
    {
        // Arrange
        var entity = new SoftDeletableTestEntity(Guid.NewGuid());

        // Act
        entity.MarkAsDeleted("admin@briefme.com");

        // Assert
        entity.IsDeleted.Should().BeTrue();
        entity.DeletedAt.Should().NotBeNull();
        entity.DeletedBy.Should().Be("admin@briefme.com");
    }
}
