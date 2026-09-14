using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Common;

namespace BriefMe.Application.Tests.TestDoubles;

public sealed class RecordingBehavior : IPipelineBehavior<TestCommand, Result<string>>
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    public RecordingBehavior(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public Task<Result<string>> Handle(TestCommand request, RequestHandlerDelegate<Result<string>> next, CancellationToken cancellationToken)
    {
        _executionOrder.Add(_name);
        return next();
    }
}

public sealed class ShortCircuitBehavior : IPipelineBehavior<TestCommand, Result<string>>
{
    public Task<Result<string>> Handle(TestCommand request, RequestHandlerDelegate<Result<string>> next, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("short-circuited"));
}
