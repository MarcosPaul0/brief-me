using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(UserErrors.Validation.EmailRequired)
            .EmailAddress().WithMessage(UserErrors.Validation.InvalidEmailFormat);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(UserErrors.Validation.PasswordRequired);
    }
}
