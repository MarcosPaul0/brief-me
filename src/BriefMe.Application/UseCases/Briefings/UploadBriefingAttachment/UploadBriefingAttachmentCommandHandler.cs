using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.UploadBriefingAttachment;

/// <summary>
/// Records a reference file attached to a briefing (RF005, BRF-013). Checks the upload against the configured
/// limits, checks the caller owns the briefing, writes the bytes through <c>IFileStorageService</c> and
/// attaches the metadata through <c>Briefing.AddAttachment()</c> — <c>Attachment</c> is a child of the
/// <c>Briefing</c> aggregate (BRF-004), so a file is created through that root and never through a repository
/// of its own.
/// </summary>
/// <remarks>
/// The steps are ordered so that nothing is written before everything that can refuse the upload has spoken:
/// the pure, cheap <c>IFileValidator</c> first, then the briefing, the caller's ownership and the status rule,
/// and only then <c>SaveAsync</c>. An oversized file or a forbidden caller therefore never fills storage, and
/// the window in which a stored blob could outlive a failed transaction is as small as the commit itself. Same
/// ordering as <c>SubmitTaskDeliveryCommandHandler</c> (BRF-031), the RF022 counterpart.
/// <para>
/// The briefing is loaded through the plain <c>GetByIdAsync</c>: <c>AddAttachment</c> only appends, and EF's
/// change tracker turns the new child into an <c>INSERT</c> without its siblings being present. Loading the
/// collection would be a join paid per upload for rows nobody reads here.
/// </para>
/// </remarks>
public sealed class UploadBriefingAttachmentCommandHandler(
    IBriefingRepository briefingRepository,
    IFileValidator fileValidator,
    IFileStorageService fileStorage,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<UploadBriefingAttachmentCommand, AttachmentDto>
{
    /// <summary>
    /// The statuses <c>Briefing.AddAttachment()</c> accepts. Mirrored here so a briefing that moved on between
    /// page load and click is an expected <c>Conflict</c> rather than a <c>DomainException</c> surfacing as a
    /// 500 (CLAUDE.md — Result over exceptions for expected failures).
    /// </summary>
    private static readonly BriefingStatus[] AttachableStatuses =
    [
        BriefingStatus.Draft,
        BriefingStatus.AwaitingApproval,
        BriefingStatus.ChangesRequested
    ];

    public async Task<Result<AttachmentDto>> Handle(
        UploadBriefingAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        // The caller is the file's recorded author, so an unauthenticated one has nothing to record and must
        // not reach storage or the repository.
        if (!Guid.TryParse(currentUser.UserId, out var uploaderId))
            return Result.Unauthorized<AttachmentDto>(CommonErrors.UserNotAuthenticated);

        // Second line of defence behind the validator, which the pipeline already ran: without it the null
        // stream would surface from SaveAsync as an ArgumentNullException, i.e. a 500 for plain user input.
        if (request.Content is null)
            return Result.Failure<AttachmentDto>(
                [new ValidationError(nameof(request.Content), BriefingErrors.Validation.FileContentRequired)]);

        // Size and type come from configuration (BRF-011), so they are the file validator's call, not the
        // command validator's. Runs before any read: refusing an oversized upload costs one comparison.
        var fileValidation = fileValidator.Validate(request.FileName ?? string.Empty, request.SizeBytes);
        if (fileValidation.IsFailure)
            return Result.Failure<AttachmentDto>(fileValidation.Errors);

        var briefing = await briefingRepository.GetByIdAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<AttachmentDto>(BriefingErrors.NotFound);

        // Attaching material is part of composing the briefing, so the guard is the one editing uses.
        if (briefing.RequesterId != uploaderId)
            return Result.Forbidden<AttachmentDto>(BriefingErrors.NotOwnerToEdit);

        if (!AttachableStatuses.Contains(briefing.Status))
            return Result.Conflict<AttachmentDto>(BriefingErrors.AttachmentNotAllowed(briefing.Status));

        // Last step before the commit, so a rejection above never leaves a blob behind. The returned key is
        // opaque (BRF-011) and is stored, never parsed or exposed.
        var storagePath = await fileStorage.SaveAsync(
            request.Content, request.FileName ?? string.Empty, cancellationToken);

        var attachment = briefing.AddAttachment(
            request.FileName ?? string.Empty, storagePath, request.SizeBytes, uploaderId);

        // No Update call: the briefing is tracked by the DbContext, so the new child of its Attachments
        // collection is picked up by change detection and CommitAsync emits the INSERT.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(AttachmentDto.FromAttachment(attachment));
    }
}
