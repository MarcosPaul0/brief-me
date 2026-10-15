using BriefMe.Application.Common.Errors;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Application.UseCases.Briefings.CreateBriefing;
using BriefMe.Domain.Entities;
using BriefMe.Domain.ValueObjects;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.CreateBriefing;

public class CreateBriefingCommandValidatorTests
{
    private readonly CreateBriefingCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenOnlyTitleCategoryAndPriorityProvided_IsValid()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenTitleIsBlank_IsInvalid(string title)
    {
        // Arrange
        var command = new CreateBriefingCommand(title, Priority.Medium, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingCommand.Title));
    }

    [Fact]
    public void Validate_WhenCategoryIdIsEmpty_IsInvalid()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", Priority.Medium, Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingCommand.CategoryId));
    }

    [Fact]
    public void Validate_WhenPriorityIsNotDefinedEnumValue_IsInvalid()
    {
        // Arrange
        var command = new CreateBriefingCommand("Summer campaign", (Priority)99, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBriefingCommand.Priority));
    }

    private static CreateBriefingCommand WithAttachments(params AttachmentUpload[] attachments) =>
        new("Summer campaign", Priority.Medium, Guid.NewGuid(), Attachments: attachments);

    [Fact]
    public void Validate_WhenAttachmentCountIsAtMaximum_IsValid()
    {
        // Arrange
        var command = WithAttachments([.. Enumerable.Range(0, Briefing.MaxAttachments).Select(_ => TestUploads.Pdf())]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenAttachmentCountExceedsMaximum_IsInvalid()
    {
        // Arrange
        var command = WithAttachments([.. Enumerable.Range(0, Briefing.MaxAttachments + 1).Select(_ => TestUploads.Pdf())]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == BriefingErrors.TooManyAttachments(Briefing.MaxAttachments));
    }

    [Fact]
    public void Validate_WhenAttachmentIsEmpty_IsInvalid()
    {
        // Arrange
        var command = WithAttachments(TestUploads.Empty("blank.pdf"));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == BriefingErrors.Validation.AttachmentEmpty("blank.pdf"));
    }

    [Theory]
    [InlineData(AttachmentFormat.MaxSizeBytes, true)]
    [InlineData(AttachmentFormat.MaxSizeBytes + 1, false)]
    public void Validate_WhenAttachmentIsAtSizeBoundary_AcceptsUpToTheLimit(long length, bool expectedValid)
    {
        // Arrange
        var command = WithAttachments(TestUploads.PdfDeclaring(length));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }
}
