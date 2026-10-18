using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Briefings.GetBriefingQueue;

public sealed class GetBriefingQueueQueryValidator : AbstractValidator<GetBriefingQueueQuery>
{
    private const int MaxPageSize = 100;

    public GetBriefingQueueQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));
    }
}
