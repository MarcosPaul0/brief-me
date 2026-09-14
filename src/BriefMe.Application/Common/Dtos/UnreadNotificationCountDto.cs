namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// How many entries of the caller's inbox are still unread (BRF-054) — the bell's badge.
/// </summary>
/// <remarks>
/// An object around a single integer, rather than a bare number on the wire: the badge is the one value this
/// route will ever carry today, and a JSON object leaves room to say more later — "how many of them are
/// overdue alerts", say — without changing the shape every existing client parses.
/// </remarks>
/// <param name="Count">Unread entries of the authenticated recipient; never negative, zero when the inbox is caught up.</param>
public sealed record UnreadNotificationCountDto(int Count);
