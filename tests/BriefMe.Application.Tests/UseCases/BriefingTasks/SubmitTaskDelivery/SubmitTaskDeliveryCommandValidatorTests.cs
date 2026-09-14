using System.Text;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;
using BriefMe.Domain.Entities;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.SubmitTaskDelivery;

public class SubmitTaskDeliveryCommandValidatorTests
{
    private readonly SubmitTaskDeliveryCommandValidator _validator = new();

    private static Stream Content() => new MemoryStream(Encoding.UTF8.GetBytes("bytes"));

    [Fact]
    public void Validate_WhenCommandIsValid_Passes()
    {
        // Arrange
        var command = new SubmitTaskDeliveryCommand(Guid.NewGuid(), "key-visual.psd", 4096, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTaskIdIsEmpty_Fails()
    {
        // Arrange
        var command = new SubmitTaskDeliveryCommand(Guid.Empty, "key-visual.psd", 4096, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.TaskRequired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenFileNameIsNullOrWhitespace_Fails(string? fileName)
    {
        // Arrange
        var command = new SubmitTaskDeliveryCommand(Guid.NewGuid(), fileName, 4096, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().Contain(e => e.ErrorMessage == BriefingTaskErrors.Validation.FileNameRequired);
    }

    [Fact]
    public void Validate_WhenFileNameExceedsTheColumnWidth_Fails()
    {
        // Arrange
        var fileName = new string('a', Attachment.MaxFileNameLength + 1);
        var command = new SubmitTaskDeliveryCommand(Guid.NewGuid(), fileName, 4096, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == BriefingTaskErrors.Validation.FileNameTooLong(Attachment.MaxFileNameLength));
    }

    [Fact]
    public void Validate_WhenContentIsNull_Fails()
    {
        // Arrange
        var command = new SubmitTaskDeliveryCommand(Guid.NewGuid(), "key-visual.psd", 4096, Content: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.FileContentRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenSizeBytesIsNotPositive_Fails(long sizeBytes)
    {
        // Arrange
        var command = new SubmitTaskDeliveryCommand(Guid.NewGuid(), "key-visual.psd", sizeBytes, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.FileSizeMustBePositive);
    }

    [Fact]
    public void Validate_WhenSizeIsAboveTheConfiguredLimit_Passes()
    {
        // Arrange — the configured maximum belongs to IFileValidator (BRF-011), which reads it from
        // Infrastructure settings; this validator only knows a delivery must carry bytes.
        var command = new SubmitTaskDeliveryCommand(Guid.NewGuid(), "key-visual.psd", long.MaxValue, Content());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
