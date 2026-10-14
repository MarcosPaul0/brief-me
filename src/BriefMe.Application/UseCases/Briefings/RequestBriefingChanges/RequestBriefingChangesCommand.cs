using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.RequestBriefingChanges;

/// <summary>The requester sends the delivery back to the same designer, saying what to change.</summary>
public sealed record RequestBriefingChangesCommand(Guid BriefingId, string? Reason) : ICommand<BriefingDto>;
