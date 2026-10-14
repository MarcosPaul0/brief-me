using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingQueue;

/// <summary>The open queue designers claim from: submitted, unclaimed briefings, oldest first.</summary>
public sealed record GetBriefingQueueQuery(int Page, int PageSize) : IQuery<PagedDto<BriefingListItemDto>>;
