using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;

/// <summary>
/// Returns the authenticated requester's own briefings, paginated, with optional filters by
/// <see cref="Status"/> and <see cref="CategoryId"/> (RF008, BRF-008). The requester is taken from
/// <c>ICurrentUser</c> and is not part of the query. Broad-view (Manager/Admin sees everyone's briefings) is
/// out of scope here — see the spec.
/// </summary>
public sealed record GetBriefingsByRequesterQuery(
    int Page,
    int PageSize,
    BriefingStatus? Status = null,
    Guid? CategoryId = null) : IQuery<PagedDto<BriefingDto>>;
