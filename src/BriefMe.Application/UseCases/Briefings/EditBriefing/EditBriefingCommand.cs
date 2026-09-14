using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.EditBriefing;

/// <summary>
/// Edits an existing briefing (RF009, BRF-009). Allowed only while in <c>Draft</c>, <c>AwaitingApproval</c> or
/// <c>ChangesRequested</c> (RF014, BRF-018), and only for the requester who owns it. The editor is the
/// authenticated user (<c>ICurrentUser</c>) and is not part of the command.
/// </summary>
public sealed record EditBriefingCommand(
    Guid BriefingId,
    string Title,
    Priority Priority,
    string? Description = null,
    string? Objective = null,
    string? RequestingArea = null,
    DateOnly? DesiredDeliveryDate = null) : ICommand<BriefingDto>;
