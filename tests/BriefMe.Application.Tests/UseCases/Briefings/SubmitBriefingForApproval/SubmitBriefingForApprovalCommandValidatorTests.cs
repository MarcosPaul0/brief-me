using BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.SubmitBriefingForApproval;

public class SubmitBriefingForApprovalCommandValidatorTests
{
    private readonly SubmitBriefingForApprovalCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdProvided_IsValid()
    {
        // Arrange
        var command = new SubmitBriefingForApprovalCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new SubmitBriefingForApprovalCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitBriefingForApprovalCommand.BriefingId));
    }
}
