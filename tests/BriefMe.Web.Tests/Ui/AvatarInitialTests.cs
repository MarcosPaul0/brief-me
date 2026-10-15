using BriefMe.Web.Ui;
using FluentAssertions;

namespace BriefMe.Web.Tests.Ui;

public class AvatarInitialTests
{
    [Theory]
    [InlineData("Ana Silva", "A")]
    [InlineData("  bruno costa", "B")]
    [InlineData("érica", "É")]
    [InlineData("Érica", "É")] // E + combining acute: the whole grapheme
    public void From_WhenNameHasText_ReturnsFirstLetterUpperCased(string name, string expected)
    {
        // Arrange
        // (name from InlineData)

        // Act
        var initial = AvatarInitial.From(name);

        // Assert
        initial.Should().Be(expected.ToUpperInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_WhenNameIsBlank_ReturnsQuestionMark(string? name)
    {
        // Arrange
        // (name from InlineData)

        // Act
        var initial = AvatarInitial.From(name);

        // Assert
        initial.Should().Be("?");
    }

    [Fact]
    public void ToneIndex_WhenCalledTwiceForSameName_ReturnsSameIndex()
    {
        // Arrange
        const string name = "Carla Mendes";

        // Act
        var first = AvatarInitial.ToneIndex(name, 5);
        var second = AvatarInitial.ToneIndex(name, 5);

        // Assert
        first.Should().Be(second);
    }

    [Theory]
    [InlineData("Ana")]
    [InlineData("Zé")]
    [InlineData(null)]
    public void ToneIndex_WhenComputed_StaysWithinToneCount(string? name)
    {
        // Arrange
        const int toneCount = 5;

        // Act
        var index = AvatarInitial.ToneIndex(name, toneCount);

        // Assert
        index.Should().BeInRange(0, toneCount - 1);
    }
}
