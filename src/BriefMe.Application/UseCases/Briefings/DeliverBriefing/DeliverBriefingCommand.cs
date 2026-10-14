using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.DeliverBriefing;

/// <summary>The assigned designer hands over the work, described by a note (typically a link to the files).</summary>
public sealed record DeliverBriefingCommand(Guid BriefingId, string? DeliveryNote) : ICommand<BriefingDto>;
