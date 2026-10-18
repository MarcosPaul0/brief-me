using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingAttachmentContent;

/// <summary>
/// Bad token, unknown briefing, an attachment that is not (or no longer) on it and a vanished object all answer
/// the same NotFound (ATT-19) — the caller learns nothing about what exists. The token is checked before any
/// read, so an unauthorized request never reaches the database.
/// </summary>
public sealed class GetBriefingAttachmentContentQueryHandler(
    IBriefingRepository briefingRepository,
    IFileStorage fileStorage,
    IAttachmentLinkSigner attachmentLinkSigner) : IQueryHandler<GetBriefingAttachmentContentQuery, AttachmentFileDto>
{
    public async Task<Result<AttachmentFileDto>> Handle(GetBriefingAttachmentContentQuery request, CancellationToken cancellationToken)
    {
        if (!attachmentLinkSigner.Verify(request.BriefingId, request.AttachmentId, request.Token))
            return Result.NotFound<AttachmentFileDto>(BriefingErrors.AttachmentNotFound);

        var briefing = await briefingRepository.GetByIdWithAttachmentsAsync(request.BriefingId, cancellationToken);
        var attachment = briefing?.Attachments.FirstOrDefault(a => a.Id == request.AttachmentId);
        if (attachment is null)
            return Result.NotFound<AttachmentFileDto>(BriefingErrors.AttachmentNotFound);

        var content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        if (content is null)
            return Result.NotFound<AttachmentFileDto>(BriefingErrors.AttachmentNotFound);

        return Result.Success(new AttachmentFileDto(content, attachment.ContentType, attachment.FileName));
    }
}
