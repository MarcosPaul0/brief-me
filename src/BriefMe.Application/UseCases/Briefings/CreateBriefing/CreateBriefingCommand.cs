using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.CreateBriefing;

/// <summary>
/// Saves a new briefing as a draft. The requester is the authenticated user (<c>ICurrentUser</c>) and is not
/// part of the command. <paramref name="Attachments"/> are optional reference files stored with the draft.
/// </summary>
public sealed record CreateBriefingCommand(
    string Title,
    Priority Priority,
    Guid CategoryId,
    string? Description = null,
    string? Objective = null,
    string? RequestingArea = null,
    DateOnly? DesiredDeliveryDate = null,
    IReadOnlyList<AttachmentUpload>? Attachments = null) : ICommand<BriefingDto>;
