using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage(UserErrors.Validation.UserIdRequired);

        RuleFor(x => x.NewPassword).Custom((newPassword, context) =>
        {
            var result = Password.Create(newPassword);
            if (result.IsFailure)
                context.AddFailure(result.Error!);
        });
    }
}
