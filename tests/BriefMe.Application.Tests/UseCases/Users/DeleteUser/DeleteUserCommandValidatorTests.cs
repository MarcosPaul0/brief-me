using BriefMe.Application.UseCases.Users.DeleteUser;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.DeleteUser;

public class DeleteUserCommandValidatorTests
{
    private readonly DeleteUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenUserIdIsValid_HasNoErrors()
    {
        // Arrange
        var command = new DeleteUserCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_HasErrorForUserId()
    {
        // Arrange
        var command = new DeleteUserCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
