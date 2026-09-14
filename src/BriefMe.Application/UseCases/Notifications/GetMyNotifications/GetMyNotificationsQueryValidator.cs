using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Notifications.GetMyNotifications;

/// <summary>
/// Paging guards for the inbox listing (BRF-054). Nothing else to validate: the query carries no filter, and
/// whose inbox it is comes from the authenticated identity, not from the request.
/// </summary>
public sealed class GetMyNotificationsQueryValidator : AbstractValidator<GetMyNotificationsQuery>
{
    private const int MaxPageSize = 100;

    public GetMyNotificationsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(CommonErrors.Paging.PageOutOfRange);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage(CommonErrors.Paging.PageSizeOutOfRange(MaxPageSize));
    }
}
