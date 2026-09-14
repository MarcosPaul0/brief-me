using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.ChangeUserStatus;

public sealed class ChangeUserStatusCommandValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage(UserErrors.Validation.UserIdRequired);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(UserErrors.Validation.InvalidStatus);
    }
}
