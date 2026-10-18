using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class CategoryTests
{
    [Fact]
    public void Create_WhenNameIsValid_StartsActiveWithTrimmedName()
    {
        // Arrange
        // (none)

        // Act
        var category = Category.Create("  Evento  ", "  Feiras e congressos ");

        // Assert
        category.Should().BeEquivalentTo(new { Name = "Evento", Description = "Feiras e congressos", IsActive = true });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenNameIsNullOrWhitespace_ThrowsDomainException(string? name)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Category.Create(name!);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenNameIsTooLong_ThrowsDomainException()
    {
        // Arrange
        var name = new string('a', Category.NameMaxLength + 1);

        // Act
        var act = () => Category.Create(name);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangeDescription_WhenBlank_ClearsIt()
    {
        // Arrange
        var category = Category.Create("Evento", "Feiras");

        // Act
        category.ChangeDescription("  ");

        // Assert
        category.Description.Should().BeNull();
    }

    [Fact]
    public void Rename_WhenNameIsValid_ReplacesIt()
    {
        // Arrange
        var category = Category.Create("Evento");

        // Act
        category.Rename("Eventos");

        // Assert
        category.Name.Should().Be("Eventos");
    }

    [Fact]
    public void Deactivate_WhenActive_MarksInactive()
    {
        // Arrange
        var category = Category.Create("Evento");

        // Act
        category.Deactivate();

        // Assert
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_WhenInactive_MarksActive()
    {
        // Arrange
        var category = Category.Create("Evento");
        category.Deactivate();

        // Act
        category.Activate();

        // Assert
        category.IsActive.Should().BeTrue();
    }
}
