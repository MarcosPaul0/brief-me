namespace BriefMe.Infrastructure.Tests.TestDoubles;

/// <summary>
/// A <see cref="TimeProvider"/> stopped at a chosen instant, so expiry tests own the clock instead of racing it.
/// A hand-written stub: <c>TimeProvider</c> is a framework class, and the rules keep mocks to project interfaces.
/// </summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
}
