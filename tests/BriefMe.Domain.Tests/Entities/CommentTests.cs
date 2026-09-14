using System.Reflection;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class CommentTests
{
    [Fact]
    public void Create_WhenDataIsValid_SetsFieldsAndStampsCreatedAt()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        // Act
        var comment = Comment.Create(taskId, authorId, "  Please use the updated palette.  ");

        // Assert
        comment.TaskId.Should().Be(taskId);
        comment.AuthorId.Should().Be(authorId);
        comment.Text.Should().Be("Please use the updated palette.");
        comment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WhenTaskIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Comment.Create(Guid.Empty, Guid.NewGuid(), "Looks good.");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenAuthorIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Comment.Create(Guid.NewGuid(), Guid.Empty, "Looks good.");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenTextIsNullOrWhitespace_ThrowsDomainException(string? text)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Comment.Create(Guid.NewGuid(), Guid.NewGuid(), text);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenTextExceedsMaxLength_ThrowsDomainException()
    {
        // Arrange
        var text = new string('a', Comment.MaxTextLength + 1);

        // Act
        var act = () => Comment.Create(Guid.NewGuid(), Guid.NewGuid(), text);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenTextIsExactlyMaxLength_Succeeds()
    {
        // Arrange
        var text = new string('a', Comment.MaxTextLength);

        // Act
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), text);

        // Assert
        comment.Text.Should().HaveLength(Comment.MaxTextLength);
    }

    [Fact]
    public void Text_HasNoPublicSetter()
    {
        // Arrange
        var property = typeof(Comment).GetProperty(
            nameof(Comment.Text),
            BindingFlags.Public | BindingFlags.Instance);

        // Act
        var publicSetter = property!.GetSetMethod(nonPublic: false);

        // Assert
        publicSetter.Should().BeNull();
    }

    [Fact]
    public void AllProperties_HaveNoPublicSetter()
    {
        // Arrange
        var properties = typeof(Comment).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act
        var withPublicSetter = properties
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .Select(p => p.Name)
            .ToArray();

        // Assert
        withPublicSetter.Should().BeEmpty();
    }
}
