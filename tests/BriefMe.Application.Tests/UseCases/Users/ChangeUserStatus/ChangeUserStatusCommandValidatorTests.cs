using BriefMe.Application.UseCases.Users.ChangeUserStatus;
using BriefMe.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.ChangeUserStatus;

public class ChangeUserStatusCommandValidatorTests
{
    private readonly ChangeUserStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_HasNoErrors()
    {
        // Arrange
        var command = new ChangeUserStatusCommand(Guid.NewGuid(), UserStatus.Inactive);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_HasErrorForUserId()
    {
        // Arrange
        var command = new ChangeUserStatusCommand(Guid.Empty, UserStatus.Active);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_WhenStatusIsNotDefinedInEnum_HasErrorForStatus()
    {
        // Arrange
        var command = new ChangeUserStatusCommand(Guid.NewGuid(), (UserStatus)99);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}
