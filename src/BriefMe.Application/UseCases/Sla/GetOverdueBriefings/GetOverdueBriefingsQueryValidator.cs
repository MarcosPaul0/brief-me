using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Sla.GetOverdueBriefings;

/// <summary>
/// Paging guards for the overdue briefing list (BRF-045). Nothing else to validate: the query carries no
/// filter, and which briefings are late is the mark the monitor wrote, not an input.
/// </summary>
public sealed class GetOverdueBriefingsQueryValidator : AbstractValidator<GetOverdueBriefingsQuery>
{
    private const int MaxPageSize = 100;

    public GetOverdueBriefingsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));
    }
}
