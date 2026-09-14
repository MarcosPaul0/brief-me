using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.ValueObjects;

public class EmailTests
{
    [Fact]
    public void Create_WhenValueHasUppercaseAndTrailingSpace_ReturnsNormalizedValue()
    {
        // Arrange
        var raw = "USER@Empresa.com ";

        // Act
        var result = Email.Create(raw);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("user@empresa.com");
    }

    [Fact]
    public void Create_WhenValueIsInvalidFormat_ReturnsFailure()
    {
        // Arrange
        var raw = "texto-invalido";

        // Act
        var result = Email.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
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
        var result = Email.Create(raw);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenSameValue_ReturnsTrue()
    {
        // Arrange
        var first = Email.Create("user@empresa.com").Value;
        var second = Email.Create("user@empresa.com").Value;

        // Act
        var result = first.Equals(second);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentValue_ReturnsFalse()
    {
        // Arrange
        var first = Email.Create("user@empresa.com").Value;
        var second = Email.Create("other@empresa.com").Value;

        // Act
        var result = first.Equals(second);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenOtherIsNull_ReturnsFalse()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;

        // Act
        var result = email.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WhenSameValue_ReturnsSameHash()
    {
        // Arrange
        var first = Email.Create("user@empresa.com").Value;
        var second = Email.Create("user@empresa.com").Value;

        // Act
        var equalHashes = first.GetHashCode() == second.GetHashCode();

        // Assert
        equalHashes.Should().BeTrue();
    }

    [Fact]
    public void ImplicitStringConversion_WhenApplied_ReturnsUnderlyingValue()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;

        // Act
        string value = email;

        // Assert
        value.Should().Be("user@empresa.com");
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsUnderlyingValue()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;

        // Act
        var value = email.ToString();

        // Assert
        value.Should().Be("user@empresa.com");
    }

    [Fact]
    public void FromPersistedValue_WhenCalled_ReturnsEmailWithoutRevalidation()
    {
        // Arrange
        var persisted = "user@empresa.com";

        // Act
        var email = Email.FromPersistedValue(persisted);

        // Assert
        email.Value.Should().Be(persisted);
    }
}
