using BriefMe.Application.Common.Errors;
using BriefMe.Domain.ValueObjects;
using FluentValidation;

namespace BriefMe.Application.UseCases.Users.UploadMyAvatar;

public sealed class UploadMyAvatarCommandValidator : AbstractValidator<UploadMyAvatarCommand>
{
    public UploadMyAvatarCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotNull().WithMessage(UserErrors.Validation.AvatarRequired);

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage(UserErrors.Validation.AvatarRequired)
            .LessThanOrEqualTo(AvatarFormat.MaxSizeBytes).WithMessage(UserErrors.Validation.AvatarTooLarge(AvatarFormat.MaxSizeBytes));
    }
}
