using BriefMe.Infrastructure.Storage;
using FluentAssertions;

namespace BriefMe.Infrastructure.Tests.Storage;

public sealed class StorageKeyTests
{
    [Fact]
    public void Generate_WhenFileNameHasExtension_PreservesIt()
    {
        // Arrange
        // (none needed)

        // Act
        var key = StorageKey.Generate("brief.pdf");

        // Assert
        key.Should().EndWith(".pdf");
    }

    [Fact]
    public void Generate_WhenCalled_PartitionsByUtcDateWithForwardSlashes()
    {
        // Arrange
        var expectedPrefix = $"{DateTime.UtcNow:yyyy/MM/dd}/";

        // Act
        var key = StorageKey.Generate("brief.pdf");

        // Assert
        key.Should().StartWith(expectedPrefix);
        key.Should().NotContain("\\");
    }

    [Fact]
    public void Generate_WhenCalledTwiceWithSameFileName_ReturnsDistinctKeys()
    {
        // Arrange
        // (none needed)

        // Act
        var first = StorageKey.Generate("brief.pdf");
        var second = StorageKey.Generate("brief.pdf");

        // Assert
        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("readme")]
    public void Generate_WhenFileNameHasNoExtension_ReturnsKeyWithoutOne(string fileName)
    {
        // Arrange
        // (none needed)

        // Act
        var key = StorageKey.Generate(fileName);

        // Assert
        key.Should().NotContain(".");
    }
}
