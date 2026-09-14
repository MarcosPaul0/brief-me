using BriefMe.Application.UseCases.Roles.GetRoleById;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Roles.GetRoleById;

public class GetRoleByIdQueryValidatorTests
{
    private readonly GetRoleByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenRoleIdIsValid_HasNoErrors()
    {
        // Arrange
        var query = new GetRoleByIdQuery(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenRoleIdIsEmpty_HasErrorForRoleId()
    {
        // Arrange
        var query = new GetRoleByIdQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RoleId);
    }
}
