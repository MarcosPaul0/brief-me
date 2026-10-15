using BriefMe.Application.Common.Errors;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Application.UseCases.Briefings.EditBriefing;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.EditBriefing;

public class EditBriefingCommandValidatorTests
{
    private readonly EditBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdTitleAndPriorityProvided_IsValid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", Priority.Medium);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.Empty, "New title", Priority.Medium);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EditBriefingCommand.BriefingId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenTitleIsBlank_IsInvalid(string title)
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), title, Priority.Medium);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EditBriefingCommand.Title));
    }

    [Fact]
    public void Validate_WhenPriorityIsNotDefinedEnumValue_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", (Priority)99);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EditBriefingCommand.Priority));
    }

    [Fact]
    public void Validate_WhenMoreNewAttachmentsThanMaximum_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", Priority.Medium,
            NewAttachments: [.. Enumerable.Range(0, Briefing.MaxAttachments + 1).Select(_ => TestUploads.Pdf())]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == BriefingErrors.TooManyAttachments(Briefing.MaxAttachments));
    }

    [Fact]
    public void Validate_WhenNewAttachmentIsOversized_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", Priority.Medium,
            NewAttachments: [TestUploads.Oversized("huge.mp4")]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenRemovedAttachmentIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new EditBriefingCommand(Guid.NewGuid(), "New title", Priority.Medium,
            RemovedAttachmentIds: [Guid.Empty]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == BriefingErrors.Validation.AttachmentIdRequired);
    }
}
