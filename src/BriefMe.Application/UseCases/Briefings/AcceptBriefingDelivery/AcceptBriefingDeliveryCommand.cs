using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.AcceptBriefingDelivery;

/// <summary>The requester accepts the designer's delivery and closes the briefing.</summary>
public sealed record AcceptBriefingDeliveryCommand(Guid BriefingId) : ICommand<BriefingDto>;
