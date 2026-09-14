using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;

namespace BriefMe.Application.UseCases.Briefings.SubmitBriefingForApproval;

/// <summary>
/// Submits a briefing for approval (RF007, BRF-007), moving it from <c>Draft</c>/<c>ChangesRequested</c> to
/// <c>AwaitingApproval</c>. The submitter is the authenticated user (<c>ICurrentUser</c>) and must own the
/// briefing; therefore only the target <see cref="BriefingId"/> is carried.
/// </summary>
public sealed record SubmitBriefingForApprovalCommand(Guid BriefingId) : ICommand<BriefingDto>;
