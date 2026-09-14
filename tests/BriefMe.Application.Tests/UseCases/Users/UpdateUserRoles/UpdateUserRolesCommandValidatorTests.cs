using BriefMe.Application.UseCases.Users.UpdateUserRoles;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.UpdateUserRoles;

public class UpdateUserRolesCommandValidatorTests
{
    private readonly UpdateUserRolesCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenUserIdIsValid_HasNoErrors()
    {
        // Arrange
        var command = new UpdateUserRolesCommand(Guid.NewGuid(), [Guid.NewGuid()]);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_HasErrorForUserId()
    {
        // Arrange
        var command = new UpdateUserRolesCommand(Guid.Empty, []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
