using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.ClaimBriefing;

/// <summary>The authenticated designer takes a briefing from the queue.</summary>
public sealed record ClaimBriefingCommand(Guid BriefingId) : ICommand<BriefingDto>;
