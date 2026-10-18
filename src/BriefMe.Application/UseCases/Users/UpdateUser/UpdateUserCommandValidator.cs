using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage(UserErrors.Validation.UserIdRequired);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(UserErrors.Validation.FullNameRequired);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(UserErrors.Validation.EmailRequired)
            .EmailAddress().WithMessage(UserErrors.Validation.InvalidEmailFormat);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage(UserErrors.Validation.InvalidRole);
    }
}
