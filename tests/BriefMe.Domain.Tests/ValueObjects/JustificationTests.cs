using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class JustificationTests
{
    [Fact]
    public void Create_WhenTextIsValid_ReturnsSuccess()
    {
        // Arrange
        const string text = "The objective does not match the requesting area.";

        // Act
        var result = Justification.Create(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenTextIsNullOrWhitespace_ReturnsFailure(string? text)
    {
        // Arrange
        // (none)

        // Act
        var result = Justification.Create(text);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenTextHasSurroundingWhitespace_TrimsValue()
    {
        // Arrange
        const string text = "  missing brand guidelines  ";

        // Act
        var result = Justification.Create(text);

        // Assert
        result.Value.Value.Should().Be("missing brand guidelines");
    }

    [Fact]
    public void Create_WhenTextIsAtMaxLength_ReturnsSuccess()
    {
        // Arrange
        var text = new string('a', Justification.MaxLength);

        // Act
        var result = Justification.Create(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenTextExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var text = new string('a', Justification.MaxLength + 1);

        // Act
        var result = Justification.Create(text);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenValuesAreEqual_ReturnsTrue()
    {
        // Arrange
        var first = Justification.Create("same reason").Value;
        var second = Justification.Create("same reason").Value;

        // Act
        var areEqual = first.Equals(second);

        // Assert
        areEqual.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenValuesDiffer_ReturnsFalse()
    {
        // Arrange
        var first = Justification.Create("one reason").Value;
        var second = Justification.Create("another reason").Value;

        // Act
        var areEqual = first.Equals(second);

        // Assert
        areEqual.Should().BeFalse();
    }
}
