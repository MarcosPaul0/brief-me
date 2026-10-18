using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;

namespace BriefMe.Application.UseCases.Briefings.SubmitBriefing;

/// <summary>
/// Sends a draft to the designer queue (<c>Draft</c> → <c>Submitted</c>). The submitter is the authenticated
/// user and must own the briefing, so only the target <see cref="BriefingId"/> is carried.
/// </summary>
public sealed record SubmitBriefingCommand(Guid BriefingId) : ICommand<BriefingDto>;
