using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingById;

/// <summary>
/// A single briefing with the names its detail screen shows. Visible to its requester, to any designer while
/// it is in the queue, and to the designer who claimed it.
/// </summary>
/// <remarks>
/// Returns <see cref="BriefingDetailDto"/>, not <see cref="BriefingDto"/>: the edit form renders the file
/// list beside the fields (RF005), and splitting that into a second endpoint would cost a round trip for
/// data the same aggregate already carries.
/// </remarks>
public sealed record GetBriefingByIdQuery(Guid BriefingId) : IQuery<BriefingDetailDto>;
