using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.RemoveBriefingAttachment;

/// <summary>
/// Removes one reference file from a briefing (RF005, BRF-013), through the aggregate root that owns it
/// (BRF-004). The counterpart of <c>UploadBriefingAttachmentCommandHandler</c> and gated the same way: only
/// the requester who owns the briefing, and only while the briefing is still theirs to compose.
/// </summary>
/// <remarks>
/// Loads through <c>GetByIdWithAttachmentsAsync</c>, unlike the upload: <c>Briefing.RemoveAttachment</c>
/// filters an in-memory list, so against an unloaded collection it would be a silent no-op reported as
/// success. That read excludes task deliveries, which is also the boundary that stops a requester from
/// deleting a designer's file by guessing its id — it simply is not in the collection, so this answers
/// <c>NotFound</c>.
/// <para>
/// The stored blob is <b>not</b> deleted: <c>IFileStorageService</c> exposes no removal (BRF-011), so the row
/// goes and the file is orphaned until a reaper exists. Recorded as a known gap in the feature spec rather
/// than worked around here.
/// </para>
/// </remarks>
public sealed class RemoveBriefingAttachmentCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveBriefingAttachmentCommand, BriefingDetailDto>
{
    /// <summary>Mirrors <c>Briefing.RemoveAttachment()</c>'s guard, for the reason given on the upload's.</summary>
    private static readonly BriefingStatus[] RemovableStatuses =
    [
        BriefingStatus.Draft,
        BriefingStatus.AwaitingApproval,
        BriefingStatus.ChangesRequested
    ];

    public async Task<Result<BriefingDetailDto>> Handle(
        RemoveBriefingAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDetailDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdWithAttachmentsAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDetailDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDetailDto>(BriefingErrors.NotOwnerToEdit);

        if (!RemovableStatuses.Contains(briefing.Status))
            return Result.Conflict<BriefingDetailDto>(BriefingErrors.AttachmentRemovalNotAllowed(briefing.Status));

        // Checked before the call because RemoveAttachment is a RemoveAll: for an unknown id it removes
        // nothing and says nothing, which would report a deletion that never happened.
        if (briefing.Attachments.All(attachment => attachment.Id != request.AttachmentId))
            return Result.NotFound<BriefingDetailDto>(BriefingErrors.AttachmentNotFound);

        briefing.RemoveAttachment(request.AttachmentId);

        // No Update call: the briefing is tracked, and the child dropped from its collection is a cascade
        // orphan (BriefingConfiguration), so CommitAsync emits the DELETE.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(BriefingDetailDto.FromBriefingWithAttachments(briefing));
    }
}
