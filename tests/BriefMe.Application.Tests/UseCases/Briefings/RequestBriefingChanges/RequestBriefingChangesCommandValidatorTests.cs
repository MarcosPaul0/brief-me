using BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Briefings.RequestBriefingChanges;

public class RequestBriefingChangesCommandValidatorTests
{
    private readonly RequestBriefingChangesCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenReasonIsBlank_HasError(string? reason)
    {
        // Arrange
        var command = new RequestBriefingChangesCommand(Guid.NewGuid(), reason);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_HasErrorForBriefingId()
    {
        // Arrange
        var command = new RequestBriefingChangesCommand(Guid.Empty, "Use the new logo");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BriefingId);
    }
}
