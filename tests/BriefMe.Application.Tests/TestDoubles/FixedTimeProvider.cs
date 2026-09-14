namespace BriefMe.Application.Tests.TestDoubles;

/// <summary>
/// A <see cref="TimeProvider"/> stopped at a chosen instant. "Is this deadline past?" is the decision the
/// SLA monitor exists to make (BRF-043), so its tests need to own the clock rather than race it.
/// </summary>
/// <remarks>
/// A hand-written stub rather than a mock: <c>TimeProvider</c> is a framework class, and CLAUDE.md keeps
/// mocking to the interfaces the project itself defines.
/// </remarks>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
}
