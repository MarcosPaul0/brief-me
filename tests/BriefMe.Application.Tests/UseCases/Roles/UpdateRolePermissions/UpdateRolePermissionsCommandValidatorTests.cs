using BriefMe.Application.UseCases.Roles.UpdateRolePermissions;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Roles.UpdateRolePermissions;

public class UpdateRolePermissionsCommandValidatorTests
{
    private readonly UpdateRolePermissionsCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenRoleIdIsValid_HasNoErrors()
    {
        // Arrange
        var command = new UpdateRolePermissionsCommand(Guid.NewGuid(), [Guid.NewGuid()]);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenRoleIdIsEmpty_HasErrorForRoleId()
    {
        // Arrange
        var command = new UpdateRolePermissionsCommand(Guid.Empty, []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RoleId);
    }
}
