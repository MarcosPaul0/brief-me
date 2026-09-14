using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.GetUsersPaged;

public sealed class GetUsersPagedQueryValidator : AbstractValidator<GetUsersPagedQuery>
{
    private const int MaxPageSize = 100;

    public GetUsersPagedQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(UserErrors.Validation.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(UserErrors.Validation.PageSizeOutOfRange(MaxPageSize));

        RuleFor(x => x.SortField)
            .IsInEnum().WithMessage(UserErrors.Validation.InvalidSortField);
    }
}
