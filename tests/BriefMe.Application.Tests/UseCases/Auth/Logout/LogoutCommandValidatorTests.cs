using BriefMe.Application.UseCases.Auth.Logout;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Auth.Logout;

public class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenSingleSessionLogoutWithRefreshToken_HasNoErrors()
    {
        // Arrange
        var command = new LogoutCommand("some-refresh-token");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenSingleSessionLogoutWithoutRefreshToken_HasErrorForRefreshToken(string refreshToken)
    {
        // Arrange
        var command = new LogoutCommand(refreshToken, AllDevices: false);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Validate_WhenAllDevicesLogoutWithoutRefreshToken_HasNoErrors()
    {
        // Arrange
        var command = new LogoutCommand(AllDevices: true);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
