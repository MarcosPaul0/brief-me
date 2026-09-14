using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;

/// <summary>
/// Breaks an approved briefing down into tasks (RF015, RF016, BRF-026). Carries a list because RF015 is
/// "one or more tasks": the Design Manager decomposes a briefing into its deliverables as a single act, so
/// the whole batch is created in one transaction. Creating a single task is this command with a one-item
/// list.
/// </summary>
/// <remarks>
/// The author is the authenticated user (<c>ICurrentUser</c>) — a Design Manager, enforced by the
/// <c>tasks.create</c> policy on the endpoint (BRF-032) — and is not part of the command.
/// </remarks>
public sealed record CreateBriefingTasksCommand(
    Guid BriefingId,
    IReadOnlyList<CreateBriefingTaskItem> Tasks) : ICommand<IReadOnlyList<BriefingTaskDto>>;

/// <summary>
/// One task of the batch. Only what RF015/RF019 need at creation time: the assignee (RF017) and the deadline
/// (RF018) arrive through their own use cases, BRF-027 and BRF-028, which own the side effects those changes
/// carry — notably the <c>TaskAssigned</c> notification of RF033.
/// </summary>
public sealed record CreateBriefingTaskItem(string Title, Priority Priority);
