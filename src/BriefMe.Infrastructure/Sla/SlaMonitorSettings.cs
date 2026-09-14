namespace BriefMe.Infrastructure.Sla;

/// <summary>
/// Configuration of the deadline monitor (RF028, BRF-043). Bound from the <c>SlaMonitor</c> section via the
/// Options pattern, mirroring <see cref="Storage.FileStorageSettings"/>.
/// </summary>
public sealed class SlaMonitorSettings
{
    public const string SectionName = "SlaMonitor";

    /// <summary>
    /// Whether the hosted service sweeps at all. Defaults to on — RF028 is a requirement, not an opt-in —
    /// and exists so a maintenance window or a test host can silence the job without removing it from DI.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// How often the sweep runs. RF028 sets no frequency; fifteen minutes is fine-grained against SLAs
    /// measured in hours (<see cref="Domain.ValueObjects.SlaDeadline"/>) and cheap against a table of open
    /// demands.
    /// </summary>
    public int IntervalMinutes { get; init; } = 15;

    /// <summary>
    /// Grace period before the first sweep. Migrations and seeding run at start-up
    /// (<c>MigrateAndSeedAsync</c>); sweeping before the schema is in place would fail a pass for nothing.
    /// </summary>
    public int StartupDelaySeconds { get; init; } = 30;

    /// <summary>Rows read per query. Bounds the query, not the pass.</summary>
    public int BatchSize { get; init; } = 200;

    /// <summary>
    /// Ceiling on how many demands of each kind one pass evaluates, so a backlog cannot be loaded into
    /// memory all at once. The remainder is swept by the next pass.
    /// </summary>
    public int MaxItemsPerRun { get; init; } = 5000;
}
