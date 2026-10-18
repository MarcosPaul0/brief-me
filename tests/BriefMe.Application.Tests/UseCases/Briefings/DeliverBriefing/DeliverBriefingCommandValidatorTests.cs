using BriefMe.Application.UseCases.Briefings.DeliverBriefing;
using BriefMe.Domain.Entities;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Briefings.DeliverBriefing;

public class DeliverBriefingCommandValidatorTests
{
    private readonly DeliverBriefingCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenNoteIsBlank_HasErrorForDeliveryNote(string? note)
    {
        // Arrange
        var command = new DeliverBriefingCommand(Guid.NewGuid(), note);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DeliveryNote);
    }

    [Fact]
    public void Validate_WhenNoteIsTooLong_HasErrorForDeliveryNote()
    {
        // Arrange
        var command = new DeliverBriefingCommand(Guid.NewGuid(), new string('a', Briefing.DeliveryNoteMaxLength + 1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DeliveryNote);
    }
}
