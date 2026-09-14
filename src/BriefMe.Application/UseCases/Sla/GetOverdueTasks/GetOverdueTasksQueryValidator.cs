using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Sla.GetOverdueTasks;

/// <summary>
/// Paging guards for the overdue task list (BRF-044). Nothing else to validate: the query carries no filter,
/// and which tasks are late is the mark the monitor wrote, not an input.
/// </summary>
public sealed class GetOverdueTasksQueryValidator : AbstractValidator<GetOverdueTasksQuery>
{
    private const int MaxPageSize = 100;

    public GetOverdueTasksQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));
    }
}
