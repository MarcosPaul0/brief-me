using BriefMe.Application.UseCases.Roles.CreateRole;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Roles.CreateRole;

public class CreateRoleCommandValidatorTests
{
    private readonly CreateRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenNameIsValid_HasNoErrors()
    {
        // Arrange
        var command = new CreateRoleCommand("Designer", "Perfil de designer");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenNameIsEmpty_HasErrorForName(string name)
    {
        // Arrange
        var command = new CreateRoleCommand(name, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
