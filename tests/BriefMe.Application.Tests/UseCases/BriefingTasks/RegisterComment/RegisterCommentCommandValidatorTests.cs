using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.RegisterComment;
using BriefMe.Domain.Entities;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.RegisterComment;

public class RegisterCommentCommandValidatorTests
{
    private readonly RegisterCommentCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_Passes()
    {
        // Arrange
        var command = new RegisterCommentCommand(Guid.NewGuid(), "Primeira versão amanhã.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTaskIdIsEmpty_Fails()
    {
        // Arrange
        var command = new RegisterCommentCommand(Guid.Empty, "Alinhado.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == BriefingTaskErrors.Validation.TaskRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenTextIsNullOrWhitespace_Fails(string? text)
    {
        // Arrange
        var command = new RegisterCommentCommand(Guid.NewGuid(), text);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().Contain(e => e.ErrorMessage == BriefingTaskErrors.Validation.CommentTextRequired);
    }

    [Fact]
    public void Validate_WhenTextExceedsMaxLength_Fails()
    {
        // Arrange
        var command = new RegisterCommentCommand(Guid.NewGuid(), new string('a', Comment.MaxTextLength + 1));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.Errors.Should().ContainSingle(e =>
            e.ErrorMessage == BriefingTaskErrors.Validation.CommentTextTooLong(Comment.MaxTextLength));
    }

    [Fact]
    public void Validate_WhenTextIsExactlyMaxLength_Passes()
    {
        // Arrange
        var command = new RegisterCommentCommand(Guid.NewGuid(), new string('a', Comment.MaxTextLength));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
