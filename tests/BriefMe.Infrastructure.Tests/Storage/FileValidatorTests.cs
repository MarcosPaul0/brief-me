using BriefMe.Domain.Common;
using BriefMe.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Tests.Storage;

public class FileValidatorTests
{
    private static FileValidator CreateValidator(FileStorageSettings? settings = null) =>
        new(Options.Create(settings ?? new FileStorageSettings
        {
            MaxSizeBytes = 1_000,
            AllowedExtensions = [".pdf", ".png"]
        }));

    [Fact]
    public void Validate_WhenSizeAndExtensionAreAllowed_ReturnsSuccess()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("brief.pdf", 500);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenExtensionCaseDiffers_ReturnsSuccess()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("BRIEF.PDF", 500);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenSizeIsNotPositive_ReturnsFailure(long sizeBytes)
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("brief.pdf", sizeBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.PropertyName == "sizeBytes");
    }

    [Fact]
    public void Validate_WhenSizeExceedsMaximum_ReturnsFailure()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("brief.pdf", 1_001);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.PropertyName == "sizeBytes");
    }

    [Fact]
    public void Validate_WhenExtensionIsNotAllowed_ReturnsFailure()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("malware.exe", 500);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.PropertyName == "fileName");
    }

    [Fact]
    public void Validate_WhenAllowedExtensionsIsEmpty_AcceptsAnyExtension()
    {
        // Arrange
        var validator = CreateValidator(new FileStorageSettings
        {
            MaxSizeBytes = 1_000,
            AllowedExtensions = []
        });

        // Act
        var result = validator.Validate("anything.xyz", 500);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenSizeAndExtensionBothInvalid_ReportsEveryViolation()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("malware.exe", 5_000);

        // Assert
        result.Errors.Should().Contain(e => e.PropertyName == "sizeBytes");
        result.Errors.Should().Contain(e => e.PropertyName == "fileName");
    }

    [Fact]
    public void Validate_WhenFileNameIsBlank_ReturnsFailure()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = validator.Validate("   ", 500);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.PropertyName == "fileName");
    }
}
