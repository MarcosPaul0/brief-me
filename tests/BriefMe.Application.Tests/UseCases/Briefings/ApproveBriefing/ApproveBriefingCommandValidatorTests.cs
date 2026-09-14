using BriefMe.Application.UseCases.Briefings.ApproveBriefing;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.ApproveBriefing;

public class ApproveBriefingCommandValidatorTests
{
    private readonly ApproveBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdProvided_IsValid()
    {
        // Arrange
        var command = new ApproveBriefingCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new ApproveBriefingCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApproveBriefingCommand.BriefingId));
    }
}
