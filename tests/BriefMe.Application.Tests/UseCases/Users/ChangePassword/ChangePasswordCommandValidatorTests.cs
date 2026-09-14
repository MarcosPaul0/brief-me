using BriefMe.Application.UseCases.Users.ChangePassword;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.ChangePassword;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_HasNoErrors()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPass1", "NewPass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenCurrentPasswordIsNull_HasNoErrors()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), null, "NewPass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_HasErrorForUserId()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.Empty, "OldPass1", "NewPass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoDigitsHere")]
    public void Validate_WhenNewPasswordFailsComplexityPolicy_HasErrorForNewPassword(string newPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPass1", newPassword);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}
