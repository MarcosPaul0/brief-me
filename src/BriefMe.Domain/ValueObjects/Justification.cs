using BriefMe.Domain.Common;

namespace BriefMe.Domain.ValueObjects;

/// <summary>
/// Reason given by a reviewer when refusing to let something move forward: rejecting a briefing or returning
/// it for changes (RF013, BRF-015). Reused by the delivery approval flow (RF044/RF047, BRF-065).
/// </summary>
public sealed class Justification : IEquatable<Justification>
{
    public const int MaxLength = 2000;

    private Justification(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Justification> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Justification>("Justification is required.");

        var normalized = value.Trim();

        if (normalized.Length > MaxLength)
            return Result.Failure<Justification>($"Justification must be at most {MaxLength} characters.");

        return Result.Success(new Justification(normalized));
    }

    public static Justification FromPersistedValue(string value) => new(value);

    public bool Equals(Justification? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Justification);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static implicit operator string(Justification justification) => justification.Value;
}
