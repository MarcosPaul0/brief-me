using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;

/// <summary>
/// The authenticated requester's own briefings, newest first, paginated and optionally filtered by status
/// and category. The requester comes from <c>ICurrentUser</c> and is not part of the query.
/// </summary>
public sealed record GetBriefingsByRequesterQuery(
    int Page,
    int PageSize,
    BriefingStatus? Status = null,
    Guid? CategoryId = null) : IQuery<PagedDto<BriefingListItemDto>>;
