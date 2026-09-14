using BriefMe.Application.Abstractions;

namespace BriefMe.Infrastructure.Tests.TestDoubles;

public class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(string? userId = null)
    {
        UserId = userId;
    }

    public string? UserId { get; }
}
