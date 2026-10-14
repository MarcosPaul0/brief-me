using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingsByDesigner;

public sealed class GetBriefingsByDesignerQueryValidator : AbstractValidator<GetBriefingsByDesignerQuery>
{
    private const int MaxPageSize = 100;

    public GetBriefingsByDesignerQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(BriefingErrors.Validation.InvalidStatus)
            .When(x => x.Status.HasValue);
    }
}
