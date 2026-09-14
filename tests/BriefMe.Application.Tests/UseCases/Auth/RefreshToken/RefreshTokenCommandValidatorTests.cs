using BriefMe.Application.UseCases.Auth.RefreshToken;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Auth.RefreshToken;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenRefreshTokenIsPresent_HasNoErrors()
    {
        // Arrange
        var command = new RefreshTokenCommand("some-refresh-token");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenRefreshTokenIsEmpty_HasErrorForRefreshToken(string refreshToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
