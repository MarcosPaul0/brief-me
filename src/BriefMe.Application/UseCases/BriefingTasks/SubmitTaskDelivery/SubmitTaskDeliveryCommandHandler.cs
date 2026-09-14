using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

// Aliased: the unqualified name would bind to the sibling namespace BriefMe.Application.UseCases.Roles.
using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;

/// <summary>
/// Records a file delivered on a task (RF022, BRF-031). Checks the upload against the configured limits,
/// checks the caller may deliver this task, writes the bytes through <c>IFileStorageService</c> and attaches
/// the metadata through <c>Briefing.AddDelivery()</c> — <c>Attachment</c> is a child of the <c>Briefing</c>
/// aggregate (BRF-004), so a delivery is created through that root and never through a repository of its own.
/// </summary>
/// <remarks>
/// The steps are ordered so that nothing is written before everything that can refuse the upload has spoken:
/// the pure, cheap <c>IFileValidator</c> first, then the task, the caller's right to deliver it and the two
/// status rules, and only then <c>SaveAsync</c>. An oversized file or a forbidden caller therefore never
/// fills storage, and the window in which a stored blob could outlive a failed transaction is as small as
/// the commit itself.
/// <para>
/// Uploading does not move the task: declaring the work ready for review is RF042 (BRF-066), a separate act,
/// exactly as starting the work is in BRF-029. Numbering the successive submissions is RF049 (BRF-074/075);
/// the author and moment RF052 asks for are recorded on the <c>Attachment</c> here.
/// </para>
/// </remarks>
public sealed class SubmitTaskDeliveryCommandHandler(
    IBriefingTaskRepository taskRepository,
    IBriefingRepository briefingRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IFileValidator fileValidator,
    IFileStorageService fileStorage,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<SubmitTaskDeliveryCommand, AttachmentDto>
{
    /// <summary>
    /// Profiles allowed to deliver a task they are not assigned to (BRF-029). The requester is absent on
    /// purpose: RF022 names the designer, and the requester's part in a delivery is approving it (RF046).
    /// </summary>
    private static readonly string[] ManagementRoles =
    [
        DomainRoles.GestorDeDesign,
        DomainRoles.Administrador
    ];

    public async Task<Result<AttachmentDto>> Handle(
        SubmitTaskDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        // The caller is the delivery's recorded author (RF052), so an unauthenticated one has nothing to
        // record and must not reach storage or the repositories.
        if (!Guid.TryParse(currentUser.UserId, out var uploaderId))
            return Result.Unauthorized<AttachmentDto>(CommonErrors.UserNotAuthenticated);

        // Second line of defence behind the validator, which the pipeline already ran: without it the null
        // stream would surface from SaveAsync as an ArgumentNullException, i.e. a 500 for plain user input.
        if (request.Content is null)
            return Result.Failure<AttachmentDto>(
                [new ValidationError(nameof(request.Content), BriefingTaskErrors.Validation.FileContentRequired)]);

        // Size and type come from configuration (BRF-011), so they are the file validator's call, not the
        // command validator's. Runs before any read: refusing an oversized upload costs one comparison.
        var fileValidation = fileValidator.Validate(request.FileName ?? string.Empty, request.SizeBytes);
        if (fileValidation.IsFailure)
            return Result.Failure<AttachmentDto>(fileValidation.Errors);

        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            return Result.NotFound<AttachmentDto>(BriefingTaskErrors.NotFound);

        if (!await CallerMayDeliverAsync(task, uploaderId, cancellationToken))
            return Result.Forbidden<AttachmentDto>(BriefingTaskErrors.NotAssignedToDeliver);

        // Approved is terminal in RF020: the work it closed takes no further file. Every other status
        // accepts one — Rejected above all, which is the correction loop of RF048.
        if (task.Status is BriefingTaskStatus.Approved)
            return Result.Conflict<AttachmentDto>(BriefingTaskErrors.DeliveryNotAllowed(task.Status));

        // The briefing is where the attachment lives (BRF-004), and it is taken from the task rather than
        // from the client: a task belongs to exactly one briefing (RF016).
        var briefing = await briefingRepository.GetByIdAsync(task.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<AttachmentDto>(BriefingErrors.NotFound);

        // Mirrors the invariant of AddDelivery(): checking it here turns an expected outcome (a cancelled
        // briefing, a stale screen) into a conflict instead of a DomainException surfacing as a 500
        // (CLAUDE.md — Result over exceptions for expected failures).
        if (briefing.Status is not BriefingStatus.InProgress)
            return Result.Conflict<AttachmentDto>(BriefingErrors.DeliveryNotAllowed(briefing.Status));

        // Last step before the commit, so a rejection above never leaves a blob behind. The returned key is
        // opaque (BRF-011) and is stored, never parsed or exposed.
        var storagePath = await fileStorage.SaveAsync(
            request.Content, request.FileName ?? string.Empty, cancellationToken);

        var delivery = briefing.AddDelivery(
            task.Id, request.FileName ?? string.Empty, storagePath, request.SizeBytes, uploaderId);

        // No Update call: the briefing is tracked by the DbContext, so the new child of its Attachments
        // collection is picked up by change detection and CommitAsync emits the INSERT.
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(AttachmentDto.FromAttachment(delivery));
    }

    /// <summary>
    /// The designer the task was assigned to (RF017, RF022) or someone with a management profile. The
    /// profile lookup only happens for callers who are not the assignee, so the common path costs no extra
    /// query; a task with no assignee falls straight through to the profile check.
    /// </summary>
    /// <remarks>
    /// Profiles are matched by id — the whole role catalog is four rows (USR-047) — rather than through the
    /// <c>UserRole.Role</c> navigation, which is only populated when the query included it. Same precedent as
    /// <c>UpdateTaskStatusCommandHandler</c> and <c>RegisterCommentCommandHandler</c>.
    /// </remarks>
    private async Task<bool> CallerMayDeliverAsync(
        BriefingTask task,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        if (task.DesignerId == callerId)
            return true;

        // Soft-deleted users are filtered out by the global query filter, so a deleted caller reads as null
        // and simply holds no management profile.
        var caller = await userRepository.GetByIdWithRolesAsync(callerId, cancellationToken);
        if (caller is null)
            return false;

        var roles = await roleRepository.GetAllAsync(cancellationToken);

        var managementRoleIds = roles
            .Where(role => ManagementRoles.Contains(role.Name))
            .Select(role => role.Id)
            .ToHashSet();

        return caller.UserRoles.Any(userRole => managementRoleIds.Contains(userRole.RoleId));
    }
}
