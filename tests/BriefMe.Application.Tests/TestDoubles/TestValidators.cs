using FluentValidation;

namespace BriefMe.Application.Tests.TestDoubles;

public sealed class AlwaysValidValidator : AbstractValidator<TestCommand>
{
}

public sealed class AlwaysInvalidValidator : AbstractValidator<TestCommand>
{
    public AlwaysInvalidValidator()
    {
        RuleFor(x => x.Input).Must(_ => false).WithMessage("Input inválido.");
    }
}

public sealed class AnotherInvalidValidator : AbstractValidator<TestCommand>
{
    public AnotherInvalidValidator()
    {
        RuleFor(x => x.Input).Must(_ => false).WithMessage("Input não atende à política.");
    }
}
