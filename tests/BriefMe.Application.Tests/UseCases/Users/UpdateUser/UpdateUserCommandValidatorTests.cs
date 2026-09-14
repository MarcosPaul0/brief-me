using BriefMe.Application.UseCases.Users.UpdateUser;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.UpdateUser;

public class UpdateUserCommandValidatorTests
{
    private readonly UpdateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_HasNoErrors()
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.NewGuid(), "Ana Silva", "ana@briefme.com", []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_HasErrorForUserId()
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.Empty, "Ana Silva", "ana@briefme.com", []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenFullNameIsEmpty_HasErrorForFullName(string fullName)
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.NewGuid(), fullName, "ana@briefme.com", []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WhenEmailIsInvalid_HasErrorForEmail(string email)
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.NewGuid(), "Ana Silva", email, []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
