using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Briefings;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingById;

/// <summary>
/// Returns a single briefing owned by the authenticated requester (BRF-013, supports RF009's edit form).
/// The requester comes from <c>ICurrentUser</c>; a briefing owned by someone else yields <c>Forbidden</c>.
/// Broad-view (Manager/Admin) is out of scope here — same decision as BRF-008/BRF-009.
/// </summary>
/// <remarks>
/// Returns <see cref="BriefingDetailDto"/>, not <see cref="BriefingDto"/>: the edit form renders the file
/// list beside the fields (RF005), and splitting that into a second endpoint would cost a round trip for
/// data the same aggregate already carries.
/// </remarks>
public sealed record GetBriefingByIdQuery(Guid BriefingId) : IQuery<BriefingDetailDto>;
