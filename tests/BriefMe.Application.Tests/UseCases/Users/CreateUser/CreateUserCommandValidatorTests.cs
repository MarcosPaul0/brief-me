using BriefMe.Application.UseCases.Users.CreateUser;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.CreateUser;

public class CreateUserCommandValidatorTests
{
    private readonly CreateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_HasNoErrors()
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", "Senha123", []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenFullNameIsEmpty_HasErrorForFullName(string fullName)
    {
        // Arrange
        var command = new CreateUserCommand(fullName, "ana@briefme.com", "Senha123", []);

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
        var command = new CreateUserCommand("Ana Silva", email, "Senha123", []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("weak")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoDigitsHere")]
    public void Validate_WhenPasswordDoesNotMeetPolicy_HasErrorForPassword(string password)
    {
        // Arrange
        var command = new CreateUserCommand("Ana Silva", "ana@briefme.com", password, []);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
