using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Sla.MonitorDeadlines;

/// <summary>
/// One pass of the deadline monitor (RF028, BRF-043): compare every open demand against the deadline it
/// carries, mark the ones past due and publish <c>DeadlineOverdue</c> for them.
/// </summary>
/// <remarks>
/// A command, not a query, because it writes: the pass is the act of recording the breach, and the
/// <see cref="SlaMonitorRunDto"/> it returns is a receipt of that write, not a read model.
/// <para>
/// Carries no time and no filter. The instant to compare against comes from the injected
/// <see cref="TimeProvider"/> — a caller able to choose "now" could mark demands late that are not — and the
/// set to sweep is defined by the repositories, not by whoever schedules the pass. What the caller does
/// choose is only how much work one pass may take on; the scheduler (BRF-043's hosted service) fills both
/// from configuration.
/// </para>
/// </remarks>
/// <param name="BatchSize">Rows read per query. Bounds the query, not the pass.</param>
/// <param name="MaxItemsPerRun">
/// Ceiling on how many demands of each kind one pass evaluates. Whatever is left over is swept by the next
/// pass — marking is incremental, so nothing is lost by stopping early.
/// </param>
public sealed record MonitorDeadlinesCommand(
    int BatchSize = 200,
    int MaxItemsPerRun = 5000) : ICommand<SlaMonitorRunDto>;
