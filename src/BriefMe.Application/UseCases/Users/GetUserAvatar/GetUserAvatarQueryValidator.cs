using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.GetUserAvatar;

public sealed class GetUserAvatarQueryValidator : AbstractValidator<GetUserAvatarQuery>
{
    public GetUserAvatarQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage(UserErrors.Validation.UserIdRequired);
    }
}
