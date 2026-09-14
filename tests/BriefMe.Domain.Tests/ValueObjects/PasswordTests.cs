using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class PasswordTests
{
    [Fact]
    public void Create_WhenValueMeetsPolicy_ReturnsSuccess()
    {
        // Arrange
        var raw = "Senha123";

        // Act
        var result = Password.Create(raw);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("Senha123");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WhenValueIsNullOrWhitespace_ReturnsFailure(string? raw)
    {
        // Arrange
        // (none needed)

        // Act
        var result = Password.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenValueIsShorterThanMinimumLength_ReturnsFailure()
    {
        // Arrange
        var raw = "Ab1";

        // Act
        var result = Password.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenValueHasNoUppercaseLetter_ReturnsFailure()
    {
        // Arrange
        var raw = "senha123";

        // Act
        var result = Password.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenValueHasNoLowercaseLetter_ReturnsFailure()
    {
        // Arrange
        var raw = "SENHA123";

        // Act
        var result = Password.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenValueHasNoDigit_ReturnsFailure()
    {
        // Arrange
        var raw = "SenhaForte";

        // Act
        var result = Password.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenSameValue_ReturnsTrue()
    {
        // Arrange
        var first = Password.Create("Senha123").Value;
        var second = Password.Create("Senha123").Value;

        // Act
        var result = first.Equals(second);

        // Assert
        result.Should().BeTrue();
    }
}
