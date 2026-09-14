using BriefMe.Domain.Common;

namespace BriefMe.Domain.ValueObjects;

public sealed class Password : IEquatable<Password>
{
    private const int MinLength = 8;

    private Password(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Password> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Password>("Senha é obrigatória.");

        if (value.Length < MinLength)
            return Result.Failure<Password>($"Senha deve ter ao menos {MinLength} caracteres.");

        if (!value.Any(char.IsUpper))
            return Result.Failure<Password>("Senha deve conter ao menos uma letra maiúscula.");

        if (!value.Any(char.IsLower))
            return Result.Failure<Password>("Senha deve conter ao menos uma letra minúscula.");

        if (!value.Any(char.IsDigit))
            return Result.Failure<Password>("Senha deve conter ao menos um número.");

        return Result.Success(new Password(value));
    }

    public bool Equals(Password? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Password);

    public override int GetHashCode() => Value.GetHashCode();
}
