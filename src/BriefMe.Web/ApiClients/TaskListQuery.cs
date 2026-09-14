using BriefMe.Domain.Enums;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Parameters the task board sends to <c>GET /tasks</c> (BRF-033). Mirrors the endpoint's query string —
/// no designer id: the assignee comes from the token, server-side.
/// </summary>
public sealed record TaskListQuery(
    int Page,
    int PageSize,
    BriefingTaskStatus? Status = null,
    Priority? Priority = null);
