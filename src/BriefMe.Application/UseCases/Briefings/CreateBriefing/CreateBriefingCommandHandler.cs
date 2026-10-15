using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Briefings;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Briefings.CreateBriefing;

/// <summary>
/// Creates a briefing in <c>Draft</c> for the authenticated requester. Checks that the category exists and is
/// active before touching the aggregate. Attachments are staged in storage before the commit and removed
/// again if the commit fails, so no stored file ever points at a briefing that does not exist (ATT-05).
/// </summary>
public sealed class CreateBriefingCommandHandler(
    IBriefingRepository briefingRepository,
    ICategoryRepository categoryRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    BriefingAttachmentStager attachmentStager) : ICommandHandler<CreateBriefingCommand, BriefingDto>
{
    public async Task<Result<BriefingDto>> Handle(CreateBriefingCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            return Result.Unauthorized<BriefingDto>(CommonErrors.UserNotAuthenticated);

        if (!await categoryRepository.ExistsActiveAsync(request.CategoryId, cancellationToken))
            return Result.NotFound<BriefingDto>(CategoryErrors.NotFoundOrInactive);

        var briefing = Briefing.Create(
            request.Title,
            request.Priority,
            request.CategoryId,
            requesterId,
            request.Description,
            request.Objective,
            request.RequestingArea,
            request.DesiredDeliveryDate);

        var staged = await attachmentStager.StageAsync(briefing.Id, request.Attachments ?? [], cancellationToken);
        if (staged.IsFailure)
            return Result.Failure<BriefingDto>(staged.Error!);

        foreach (var attachment in staged.Value)
            briefing.AddAttachment(attachment.FileName, attachment.StorageKey, attachment.Format, attachment.SizeBytes);

        await briefingRepository.AddAsync(briefing, cancellationToken);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await attachmentStager.DiscardAsync(staged.Value, CancellationToken.None);
            throw;
        }

        return Result.Success(BriefingDto.FromBriefing(briefing));
    }
}
