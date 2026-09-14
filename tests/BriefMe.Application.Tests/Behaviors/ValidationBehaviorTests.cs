using BriefMe.Application.Behaviors;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Domain.Common;
using FluentAssertions;
using FluentValidation;

namespace BriefMe.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    private static Task<Result<string>> Handler() => Task.FromResult(Result.Success("handled"));

    [Fact]
    public async Task Handle_WhenNoValidatorsAreRegistered_CallsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, Result<string>>([]);

        // Act
        var result = await behavior.Handle(new TestCommand("value"), Handler, default);

        // Assert
        result.Value.Should().Be("handled");
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_CallsNext()
    {
        // Arrange
        IValidator<TestCommand>[] validators = [new AlwaysValidValidator()];
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        // Act
        var result = await behavior.Handle(new TestCommand("value"), Handler, default);

        // Assert
        result.Value.Should().Be("handled");
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationFailureWithoutCallingHandler()
    {
        // Arrange
        var handlerCalled = false;
        Task<Result<string>> Next()
        {
            handlerCalled = true;
            return Handler();
        }

        IValidator<TestCommand>[] validators = [new AlwaysInvalidValidator()];
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        // Act
        var result = await behavior.Handle(new TestCommand("value"), Next, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TestCommand.Input));
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenMultipleValidatorsFail_AggregatesAllErrors()
    {
        // Arrange
        IValidator<TestCommand>[] validators = [new AlwaysInvalidValidator(), new AnotherInvalidValidator()];
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        // Act
        var result = await behavior.Handle(new TestCommand("value"), Handler, default);

        // Assert
        result.Errors.Should().HaveCount(2);
    }
}
