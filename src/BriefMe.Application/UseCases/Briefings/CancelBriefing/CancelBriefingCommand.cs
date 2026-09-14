using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.CancelBriefing;

public sealed record CancelBriefingCommand(Guid BriefingId) : ICommand<BriefingDto>;