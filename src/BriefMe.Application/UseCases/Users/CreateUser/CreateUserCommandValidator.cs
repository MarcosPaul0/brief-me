using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(UserErrors.Validation.FullNameRequired);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(UserErrors.Validation.EmailRequired)
            .EmailAddress().WithMessage(UserErrors.Validation.InvalidEmailFormat);

        RuleFor(x => x.Password).Custom((password, context) =>
        {
            var result = Password.Create(password);
            if (result.IsFailure)
                context.AddFailure(result.Error!);
        });
    }
}
