using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.EditBriefing;

/// <summary>
/// Edits a briefing while nobody is working on it (<c>Draft</c> or <c>Submitted</c>), and only for the
/// requester who owns it. The editor is the authenticated user and is not part of the command.
/// </summary>
public sealed record EditBriefingCommand(
    Guid BriefingId,
    string Title,
    Priority Priority,
    string? Description = null,
    string? Objective = null,
    string? RequestingArea = null,
    DateOnly? DesiredDeliveryDate = null) : ICommand<BriefingDto>;
