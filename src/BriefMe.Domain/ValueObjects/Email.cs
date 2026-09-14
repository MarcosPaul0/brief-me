using System.Net.Mail;
using BriefMe.Domain.Common;

namespace BriefMe.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Email>("E-mail é obrigatório.");

        var normalized = value.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalized, out _))
            return Result.Failure<Email>("E-mail em formato inválido.");

        return Result.Success(new Email(normalized));
    }

    public static Email FromPersistedValue(string value) => new(value);

    public bool Equals(Email? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;
}
