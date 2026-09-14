using BriefMe.Domain.Common;

namespace BriefMe.Domain.ValueObjects;

/// <summary>
/// How long a demand is allowed to take, in hours, as configured in an SLA rule (RF027, BRF-041).
/// </summary>
/// <remarks>
/// A duration, not a date: RF027 configures policy ("how long"), and the actual due instant only exists once
/// the policy meets a demand that has a starting point — that is <see cref="DueAtFrom"/>, which the deadline
/// monitor of RF028 (BRF-043) compares against the clock.
/// </remarks>
public sealed class SlaDeadline : IEquatable<SlaDeadline>
{
    /// <summary>An SLA of zero hours would be breached the moment the demand is opened.</summary>
    public const int MinHours = 1;

    /// <summary>
    /// One year. An SLA is a delivery commitment, not a horizon: the ceiling catches the extra zero typed
    /// into a form before it becomes a rule that never comes due.
    /// </summary>
    public const int MaxHours = 8760;

    private SlaDeadline(int hours)
    {
        Hours = hours;
    }

    public int Hours { get; }

    public static Result<SlaDeadline> Create(int hours)
    {
        if (hours < MinHours)
            return Result.Failure<SlaDeadline>($"SLA deadline must be at least {MinHours} hour.");

        if (hours > MaxHours)
            return Result.Failure<SlaDeadline>($"SLA deadline must be at most {MaxHours} hours.");

        return Result.Success(new SlaDeadline(hours));
    }

    public static SlaDeadline FromPersistedValue(int hours) => new(hours);

    /// <summary>
    /// The instant this deadline comes due for a demand that started at <paramref name="startedAtUtc"/> —
    /// running hours, no business calendar (see <c>.specs/features/sla-modelagem/spec.md</c>, decision #6).
    /// Pure: the caller owns the clock.
    /// </summary>
    public DateTime DueAtFrom(DateTime startedAtUtc) => startedAtUtc.AddHours(Hours);

    public bool Equals(SlaDeadline? other) => other is not null && Hours == other.Hours;

    public override bool Equals(object? obj) => Equals(obj as SlaDeadline);

    public override int GetHashCode() => Hours.GetHashCode();

    public override string ToString() => $"{Hours}h";

    public static implicit operator int(SlaDeadline deadline) => deadline.Hours;
}
