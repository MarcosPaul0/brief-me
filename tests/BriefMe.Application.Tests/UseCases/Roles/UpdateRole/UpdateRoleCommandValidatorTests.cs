using BriefMe.Application.UseCases.Roles.UpdateRole;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Roles.UpdateRole;

public class UpdateRoleCommandValidatorTests
{
    private readonly UpdateRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_HasNoErrors()
    {
        // Arrange
        var command = new UpdateRoleCommand(Guid.NewGuid(), "Designer Sênior", "Descrição");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenRoleIdIsEmpty_HasErrorForRoleId()
    {
        // Arrange
        var command = new UpdateRoleCommand(Guid.Empty, "Designer Sênior", null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RoleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenNameIsEmpty_HasErrorForName(string name)
    {
        // Arrange
        var command = new UpdateRoleCommand(Guid.NewGuid(), name, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
