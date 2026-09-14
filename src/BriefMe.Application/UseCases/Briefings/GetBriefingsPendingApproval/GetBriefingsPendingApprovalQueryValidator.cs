using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;

public sealed class GetBriefingsPendingApprovalQueryValidator : AbstractValidator<GetBriefingsPendingApprovalQuery>
{
    private const int MaxPageSize = 100;

    public GetBriefingsPendingApprovalQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));
    }
}
