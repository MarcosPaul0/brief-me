using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// How far a briefing has come, on the wire (RF023, RF059): the <see cref="BriefingProgress"/> value object
/// as the outer layers see it. Produced by the tracking read (BRF-039) and reused by the standalone
/// percentage query when it arrives (BRF-086).
/// </summary>
/// <remarks>
/// Carries the two counters as well as the percentage for the same reason the VO does: the screen shows
/// "2 de 4 tarefas (50%)", and a bare number would make the component count the task list again — which is
/// exactly the calculation that must stay in the domain (BRF-035).
/// <para>
/// <see cref="IsComplete"/> travels too rather than being derived by comparing <see cref="Percentage"/>
/// with 100: the percentage is a rounded display value, and 199 of 200 approved tasks deliberately reads
/// 99.99, never 100.
/// </para>
/// </remarks>
public sealed record BriefingProgressDto(
    int TotalTasks,
    int ApprovedTasks,
    decimal Percentage,
    bool IsComplete)
{
    public static BriefingProgressDto FromBriefingProgress(BriefingProgress progress)
    {
        return new(
            progress.TotalTasks,
            progress.ApprovedTasks,
            progress.Percentage,
            progress.IsComplete);
    }
}
