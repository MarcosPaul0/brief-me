namespace BriefMe.Domain.Common;

public interface IResult<TSelf> where TSelf : IResult<TSelf>
{
    static abstract TSelf ValidationFailure(IReadOnlyList<ValidationError> errors);
}

public class Result : IResult<Result>
{
    private const string ValidationFailureMessage = "Validation failed.";

    protected Result(bool isSuccess, string? error, IReadOnlyList<ValidationError>? errors = null, ErrorType type = ErrorType.Failure)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? [];
        Type = type;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Error { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    public ErrorType Type { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(string error, ErrorType type = ErrorType.Failure) => new(false, error, type: type);

    public static Result Failure(IReadOnlyList<ValidationError> errors) => new(false, ValidationFailureMessage, errors);

    public static Result ValidationFailure(IReadOnlyList<ValidationError> errors) => Failure(errors);

    public static Result NotFound(string error) => Failure(error, ErrorType.NotFound);

    public static Result Conflict(string error) => Failure(error, ErrorType.Conflict);

    public static Result Unauthorized(string error) => Failure(error, ErrorType.Unauthorized);

    public static Result Forbidden(string error) => Failure(error, ErrorType.Forbidden);

    public static Result<T> Success<T>(T value) => new(value, true, null);

    public static Result<T> Failure<T>(string error, ErrorType type = ErrorType.Failure) => new(default, false, error, type: type);

    public static Result<T> Failure<T>(IReadOnlyList<ValidationError> errors) => new(default, false, ValidationFailureMessage, errors);

    public static Result<T> NotFound<T>(string error) => Failure<T>(error, ErrorType.NotFound);

    public static Result<T> Conflict<T>(string error) => Failure<T>(error, ErrorType.Conflict);

    public static Result<T> Unauthorized<T>(string error) => Failure<T>(error, ErrorType.Unauthorized);

    public static Result<T> Forbidden<T>(string error) => Failure<T>(error, ErrorType.Forbidden);
}

public class Result<T> : Result, IResult<Result<T>>
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, string? error, IReadOnlyList<ValidationError>? errors = null, ErrorType type = ErrorType.Failure)
        : base(isSuccess, error, errors, type)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public new static Result<T> ValidationFailure(IReadOnlyList<ValidationError> errors) => Failure<T>(errors);
}
