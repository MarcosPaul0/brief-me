using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.BriefingTasks.GetTasksByDesigner;

/// <summary>
/// Paging and enum-range guards for the task board query (BRF-033). Who the tasks belong to is not
/// validated: it comes from <c>ICurrentUser</c>, never from the request.
/// </summary>
public sealed class GetTasksByDesignerQueryValidator : AbstractValidator<GetTasksByDesignerQuery>
{
    private const int MaxPageSize = 100;

    public GetTasksByDesignerQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(BriefingTaskErrors.Validation.InvalidStatus)
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(BriefingTaskErrors.Validation.InvalidPriority)
            .When(x => x.Priority.HasValue);
    }
}
