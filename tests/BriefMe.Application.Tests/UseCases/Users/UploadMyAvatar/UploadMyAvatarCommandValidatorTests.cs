using BriefMe.Application.UseCases.Users.UploadMyAvatar;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.UploadMyAvatar;

public class UploadMyAvatarCommandValidatorTests
{
    private readonly UploadMyAvatarCommandValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(AvatarFormat.MaxSizeBytes)]
    public void Validate_WhenLengthIsWithinLimit_HasNoErrors(long length)
    {
        // Arrange
        var command = new UploadMyAvatarCommand(Stream.Null, length);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(AvatarFormat.MaxSizeBytes + 1)]
    public void Validate_WhenLengthIsOutOfRange_HasErrorForLength(long length)
    {
        // Arrange
        var command = new UploadMyAvatarCommand(Stream.Null, length);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Length);
    }
}
