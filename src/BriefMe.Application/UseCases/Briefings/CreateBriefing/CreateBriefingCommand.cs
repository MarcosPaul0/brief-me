using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.CreateBriefing;

/// <summary>
/// Registers and saves a briefing as a draft (RF004, RF005, RF006). The requester is the authenticated user
/// (<c>ICurrentUser</c>) and is not part of the command. Attachments are uploaded via a dedicated use case
/// (BRF-011).
/// </summary>
public sealed record CreateBriefingCommand(
    string Title,
    Priority Priority,
    Guid CategoryId,
    string? Description = null,
    string? Objective = null,
    string? RequestingArea = null,
    DateOnly? DesiredDeliveryDate = null) : ICommand<BriefingDto>;
