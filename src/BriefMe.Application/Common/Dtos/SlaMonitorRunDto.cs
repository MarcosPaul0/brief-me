namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// What one pass of the deadline monitor did (RF028, BRF-043). Not a read model of any entity: it is the
/// receipt of a background sweep, written to the log by the hosted service and asserted on by the tests that
/// pin the sweep's behaviour.
/// </summary>
/// <remarks>
/// Scanned counts every demand the pass evaluated; marked counts the subset found past its due instant. A
/// pass with <c>Scanned &gt; 0</c> and <c>Marked == 0</c> is the healthy steady state — everything on time —
/// while two consecutive passes both marking the same order of magnitude is a sign the mark is not sticking.
/// </remarks>
/// <param name="EvaluatedAtUtc">The instant every deadline in this pass was compared against.</param>
/// <param name="TasksScanned">Tasks still open and unmarked that the pass evaluated.</param>
/// <param name="TasksMarkedOverdue">Tasks this pass found late (RF029).</param>
/// <param name="BriefingsScanned">Briefings still open and unmarked that the pass evaluated.</param>
/// <param name="BriefingsMarkedOverdue">Briefings this pass found late (RF030).</param>
public sealed record SlaMonitorRunDto(
    DateTime EvaluatedAtUtc,
    int TasksScanned,
    int TasksMarkedOverdue,
    int BriefingsScanned,
    int BriefingsMarkedOverdue);
