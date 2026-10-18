using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByDesigner;

/// <summary>
/// The briefings the authenticated designer claimed, optionally filtered by status. The designer comes from
/// <c>ICurrentUser</c> and is not part of the query.
/// </summary>
public sealed record GetBriefingsByDesignerQuery(int Page, int PageSize, BriefingStatus? Status = null)
    : IQuery<PagedDto<BriefingListItemDto>>;
