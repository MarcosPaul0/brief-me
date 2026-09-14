using BriefMe.Application.Common.Errors;
using FluentValidation;

namespace BriefMe.Application.UseCases.Roles.UpdateRole;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEqual(Guid.Empty).WithMessage(RoleErrors.Validation.RoleIdRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(RoleErrors.Validation.NameRequired);
    }
}
