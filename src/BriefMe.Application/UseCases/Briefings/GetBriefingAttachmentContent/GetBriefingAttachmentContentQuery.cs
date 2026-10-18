using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingAttachmentContent;

/// <summary>
/// An attachment's bytes for the anonymous content endpoint (ATT-18). <paramref name="Token"/> — issued by the
/// briefing detail read — is the only authorization, so there is no validator: a missing or bad token must
/// answer NotFound like any other miss, not a 400 that would confirm the attachment exists.
/// </summary>
public sealed record GetBriefingAttachmentContentQuery(Guid BriefingId, Guid AttachmentId, string? Token) : IQuery<AttachmentFileDto>;
