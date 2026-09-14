using BriefMe.Application.UseCases.Auth.Login;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Auth.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenEmailAndPasswordAreValid_HasNoErrors()
    {
        // Arrange
        var command = new LoginCommand("ana@briefme.com", "any-password");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-email")]
    public void Validate_WhenEmailIsInvalid_HasErrorForEmail(string email)
    {
        // Arrange
        var command = new LoginCommand(email, "any-password");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenPasswordIsEmpty_HasErrorForPassword(string password)
    {
        // Arrange
        var command = new LoginCommand("ana@briefme.com", password);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
