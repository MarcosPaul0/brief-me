using BriefMe.Application.Abstractions;
using BriefMe.Domain.Enums;

namespace BriefMe.Infrastructure.Tests.TestDoubles;

public class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(string? userId = null)
    {
        UserId = userId;
    }

    public string? UserId { get; }

    public UserRole? Role => null;
}
