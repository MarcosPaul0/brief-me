using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.EditBriefing;

/// <summary>
/// Edits a briefing owned by the authenticated requester. A claimed or closed briefing yields an
/// <c>EditNotAllowed</c> conflict. Every refusal happens before a byte is stored; new files are staged before
/// the commit (and discarded if it fails), removed ones are deleted only after it succeeds (ATT-08..12).
/// </summary>
public sealed class EditBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    BriefingAttachmentStager attachmentStager,
    IFileStorage fileStorage) : ICommandHandler<EditBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(EditBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        var briefing = await briefingRepository.GetByIdWithAttachmentsAsync(request.BriefingId, cancellationToken);
        if (briefing is null)
            return Result.NotFound<BriefingDto>(BriefingErrors.NotFound);

        if (briefing.RequesterId != requesterId)
            return Result.Forbidden<BriefingDto>(BriefingErrors.NotOwnerToEdit);

        if (!briefing.IsOpenForRequester)
            return Result.Conflict<BriefingDto>(BriefingErrors.EditNotAllowed);

        var removedIds = (request.RemovedAttachmentIds ?? []).Distinct().ToList();
        var newAttachments = request.NewAttachments ?? [];

        if (removedIds.Any(id => briefing.Attachments.All(attachment => attachment.Id != id)))
            return Result.NotFound<BriefingDto>(BriefingErrors.AttachmentNotFound);

        if (briefing.Attachments.Count - removedIds.Count + newAttachments.Count > Briefing.MaxAttachments)
            return Result.Failure<BriefingDto>(BriefingErrors.TooManyAttachments(Briefing.MaxAttachments));

        var staged = await attachmentStager.StageAsync(briefing.Id, newAttachments, cancellationToken);
        if (staged.IsFailure)
            return Result.Failure<BriefingDto>(staged.Error!);

        briefing.Edit(
            request.Title,
            request.Priority,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate);

        // Before the collection changes: Update marks every reachable entity as existing, and a new attachment
        // must instead be discovered as Added when the commit detects changes.
        briefingRepository.Update(briefing);

        var removedKeys = removedIds.Select(id => briefing.RemoveAttachment(id).StorageKey).ToList();

        foreach (var attachment in staged.Value)
            briefing.AddAttachment(attachment.FileName, attachment.StorageKey, attachment.Format, attachment.SizeBytes);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await attachmentStager.DiscardAsync(staged.Value, CancellationToken.None);
            throw;
        }

        foreach (var key in removedKeys)
            await fileStorage.DeleteAsync(key, cancellationToken);

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
