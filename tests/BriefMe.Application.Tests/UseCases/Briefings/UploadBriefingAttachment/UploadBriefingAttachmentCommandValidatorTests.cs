using System.Text;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;
using BriefMe.Domain.Entities;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.UploadBriefingAttachment;

public class UploadBriefingAttachmentCommandValidatorTests
{
    private readonly UploadBriefingAttachmentCommandValidator _validator = new();

    private static Stream Content() => new MemoryStream(Encoding.UTF8.GetBytes("bytes"));

    [Fact]
    public void Validate_WhenCommandIsValid_Passes()
    {
        // Arrange
        var command = new UploadBriefingAttachmentCommand(Guid.NewGuid(), "referencia.pdf", 2048, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_Fails()
    {
        // Arrange
        var command = new UploadBriefingAttachmentCommand(Guid.Empty, "referencia.pdf", 2048, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingErrors.Validation.BriefingRequired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenFileNameIsNullOrWhitespace_Fails(string? fileName)
    {
        // Arrange
        var command = new UploadBriefingAttachmentCommand(Guid.NewGuid(), fileName, 2048, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().Contain(e => e.ErrorMessage == BriefingErrors.Validation.FileNameRequired);
    }

    [Fact]
    public void Validate_WhenFileNameExceedsTheColumnWidth_Fails()
    {
        // Arrange
        var fileName = new string('a', Attachment.MaxFileNameLength + 1);
        var command = new UploadBriefingAttachmentCommand(Guid.NewGuid(), fileName, 2048, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == BriefingErrors.Validation.FileNameTooLong(Attachment.MaxFileNameLength));
    }

    [Fact]
    public void Validate_WhenContentIsNull_Fails()
    {
        // Arrange
        var command = new UploadBriefingAttachmentCommand(Guid.NewGuid(), "referencia.pdf", 2048, Content: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingErrors.Validation.FileContentRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenSizeBytesIsNotPositive_Fails(long sizeBytes)
    {
        // Arrange
        var command = new UploadBriefingAttachmentCommand(Guid.NewGuid(), "referencia.pdf", sizeBytes, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingErrors.Validation.FileSizeMustBePositive);
    }

    [Fact]
    public void Validate_WhenSizeIsAboveTheConfiguredLimit_Passes()
    {
        // Arrange — the configured maximum belongs to IFileValidator (BRF-011), which reads it from
        // Infrastructure settings; this validator only knows an attachment must carry bytes.
        var command = new UploadBriefingAttachmentCommand(Guid.NewGuid(), "referencia.pdf", long.MaxValue, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
