using BriefMe.Domain.Common;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_WhenCalled_ReturnsSuccessResultWithValue()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Value_WhenResultIsFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Failure<int>("boom");

        // Act
        var act = () => result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValidationFailure_WhenCalledOnResult_ReturnsFailureWithErrors()
    {
        // Arrange
        var errors = new List<ValidationError> { new("Email", "E-mail é obrigatório.") };

        // Act
        var result = Result.ValidationFailure(errors);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void ValidationFailure_WhenCalledOnGenericResult_ReturnsFailureWithErrors()
    {
        // Arrange
        var errors = new List<ValidationError> { new("Email", "E-mail é obrigatório.") };

        // Act
        var result = Result<int>.ValidationFailure(errors);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Failure_WhenCalledWithoutType_DefaultsToFailureErrorType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Failure("boom");

        // Assert
        result.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void NotFound_WhenCalled_ReturnsFailureWithNotFoundType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.NotFound("Usuário não encontrado.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Usuário não encontrado.");
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Conflict_WhenCalled_ReturnsFailureWithConflictType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Conflict("E-mail já cadastrado.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Unauthorized_WhenCalled_ReturnsFailureWithUnauthorizedType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Unauthorized("E-mail ou senha inválidos.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public void Forbidden_WhenCalled_ReturnsFailureWithForbiddenType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Forbidden("Acesso negado.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void NotFound_WhenCalledOnGenericResult_ReturnsFailureWithNotFoundType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.NotFound<int>("Recurso não encontrado.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Conflict_WhenCalledOnGenericResult_ReturnsFailureWithConflictType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Conflict<int>("Nome já cadastrado.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Unauthorized_WhenCalledOnGenericResult_ReturnsFailureWithUnauthorizedType()
    {
        // Arrange
        // (none needed)

        // Act
        var result = Result.Unauthorized<int>("Credenciais inválidas.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Type.Should().Be(ErrorType.Unauthorized);
    }
}
