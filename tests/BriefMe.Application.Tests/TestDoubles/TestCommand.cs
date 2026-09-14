using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Common;

namespace BriefMe.Application.Tests.TestDoubles;

public sealed record TestCommand(string Input) : ICommand<string>;

public sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
{
    public Task<Result<string>> Handle(TestCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success($"handled:{request.Input}"));
}
