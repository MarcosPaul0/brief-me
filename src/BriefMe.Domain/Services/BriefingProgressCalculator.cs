using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Domain.Services;

/// <summary>
/// Turns the tasks of a briefing into its progress (RF023), the value the completion percentage of RF059
/// (BRF-086) and the tracking screen (BRF-039) are built from.
/// </summary>
/// <remarks>
/// A domain service rather than a method on <see cref="Briefing"/>: <see cref="BriefingTask"/> is an aggregate
/// of its own (BRF-023) with its own repository (BRF-025), so the briefing never holds the collection. A rule
/// that spans two aggregates lives between them.
/// <para>
/// Pure: no repository, no clock, no state. The caller loads the tasks — through
/// <see cref="Repositories.IBriefingTaskRepository.GetByBriefingIdAsync"/>, which is unpaginated precisely so
/// this division sees the whole set — and the same input always yields the same result.
/// </para>
/// </remarks>
public sealed class BriefingProgressCalculator
{
    /// <summary>
    /// Progress of a briefing: its tasks in <see cref="BriefingTaskStatus.Approved"/> over all of its tasks
    /// (BRF-035). Every other status of RF020 counts in the total only — a task awaiting approval, blocked, or
    /// returned for correction (RF048) is work still owed, so it lowers the percentage instead of shrinking
    /// the denominator.
    /// </summary>
    /// <param name="tasks">
    /// The tasks of one briefing. Whatever is passed is what gets counted: selecting the right briefing's
    /// tasks belongs to the caller that loaded them, and a pure calculation has no way to tell which briefing
    /// was meant. An empty collection is a briefing not broken down yet (BRF-026) and yields 0%.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tasks"/> is null. "No tasks" is an empty collection; null is a caller error.
    /// </exception>
    public BriefingProgress Calculate(IReadOnlyCollection<BriefingTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var approvedTasks = tasks.Count(task => task.Status is BriefingTaskStatus.Approved);

        return BriefingProgress.FromCounts(tasks.Count, approvedTasks);
    }
}
